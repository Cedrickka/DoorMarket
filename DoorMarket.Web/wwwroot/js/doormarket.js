window.doorMarket = window.doorMarket || {};
window.doorMarket.isMobileViewport = function () {
    return window.matchMedia("(max-width: 768px)").matches;
};

window.doorMarket.downloadText = function (filename, contentType, content) {
    const blob = new Blob([content], { type: contentType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
};

window.doorMarket.copyText = async function (text) {
    if (!text) {
        return false;
    }

    try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }
    } catch {
        // fallback below
    }

    const input = document.createElement("textarea");
    input.value = text;
    input.setAttribute("readonly", "");
    input.style.position = "fixed";
    input.style.opacity = "0";
    document.body.appendChild(input);
    input.select();

    const copied = document.execCommand("copy");
    document.body.removeChild(input);
    return copied;
};

window.dmScrollTo = (id) => {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: "smooth", block: "start" });
};

window.doorMarket.initMediaProxy = function (config) {
    const enabled = !!(config && config.enabled !== false);
    if (!enabled) {
        return;
    }

    let apiHost = "";
    let apiBaseUrl = "";
    if (config && config.apiBaseUrl) {
        apiBaseUrl = String(config.apiBaseUrl);
        try {
            apiHost = new URL(apiBaseUrl).host;
        } catch {
            apiHost = "";
        }
    }

    if (!apiHost) {
        return;
    }

    const toProxy = (url) => {
        if (!url) {
            return null;
        }

        if (url.startsWith("/media/proxy?url=")) {
            return null;
        }

        const trimmed = String(url).trim();
        if (!trimmed) {
            return null;
        }

        if (trimmed.startsWith("data:") || trimmed.startsWith("blob:")) {
            return null;
        }

        let targetUrl = url;
        try {
            const isApiRelative =
                (trimmed.startsWith("/uploads/") || trimmed.startsWith("uploads/")) && apiBaseUrl;

            if (isApiRelative) {
                targetUrl = new URL(trimmed.startsWith("/") ? trimmed : "/" + trimmed, apiBaseUrl).toString();
            }

            const u = new URL(targetUrl, window.location.origin);
            if (u.host !== apiHost) {
                return null;
            }

            u.protocol = "http:";
            if (u.port === "443") {
                u.port = "";
            }

            const encoded = encodeURIComponent(u.toString());
            return "/media/proxy?url=" + encoded;
        } catch {
            return null;
        }
    };

    const processSrcset = (el) => {
        if (!el || !el.getAttribute) {
            return;
        }

        const srcset = el.getAttribute("srcset");
        if (!srcset) {
            return;
        }

        const parts = srcset.split(",").map((part) => part.trim()).filter(Boolean);
        if (!parts.length) {
            return;
        }

        const mapped = parts.map((part) => {
            const tokens = part.split(/\s+/);
            const rawUrl = tokens[0];
            const proxied = toProxy(rawUrl);
            const finalUrl = proxied || rawUrl;
            if (tokens.length > 1) {
                return [finalUrl, ...tokens.slice(1)].join(" ");
            }
            return finalUrl;
        });

        const newSrcset = mapped.join(", ");
        if (newSrcset !== srcset) {
            el.setAttribute("srcset", newSrcset);
        }
    };

    const processElement = (el) => {
        if (!el || !el.getAttribute) {
            return;
        }

        const src = el.getAttribute("src");
        const proxied = toProxy(src || "");
        if (proxied && src !== proxied) {
            el.setAttribute("src", proxied);
        }

        processSrcset(el);
    };

    const processAll = (root) => {
        const scope = root || document;
        const elements = scope.querySelectorAll ? scope.querySelectorAll("img, source") : [];
        for (const el of elements) {
            processElement(el);
        }
    };

    processAll(document);

    const observer = new MutationObserver((mutations) => {
        for (const mutation of mutations) {
            if (mutation.type === "attributes" && mutation.target) {
                if ((mutation.attributeName === "src" || mutation.attributeName === "srcset") &&
                    (mutation.target.tagName === "IMG" || mutation.target.tagName === "SOURCE")) {
                    processElement(mutation.target);
                }
                continue;
            }

            if (mutation.addedNodes && mutation.addedNodes.length) {
                mutation.addedNodes.forEach((node) => {
                    if (node.nodeType !== 1) {
                        return;
                    }

                    if (node.tagName === "IMG") {
                        processElement(node);
                    } else if (node.tagName === "SOURCE") {
                        processElement(node);
                    } else {
                        processAll(node);
                    }
                });
            }
        }
    });

    observer.observe(document.body, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["src"]
    });
};
