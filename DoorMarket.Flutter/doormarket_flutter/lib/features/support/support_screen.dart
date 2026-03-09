import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_ghost_button.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';

class SupportScreen extends ConsumerStatefulWidget {
  final String? prefillSubject;
  final String? prefillMessage;
  final bool openContactForm;

  const SupportScreen({
    super.key,
    this.prefillSubject,
    this.prefillMessage,
    this.openContactForm = false,
  });

  @override
  ConsumerState<SupportScreen> createState() => _SupportScreenState();
}

class _SupportScreenState extends ConsumerState<SupportScreen> {
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _subjectController = TextEditingController();
  final _messageController = TextEditingController();
  bool _loading = false;
  bool _showForm = false;
  String? _result;

  final _sectionKeys = <String, GlobalKey>{
    'intro': GlobalKey(),
    'how': GlobalKey(),
    'products': GlobalKey(),
    'cart': GlobalKey(),
    'delivery': GlobalKey(),
    'payments': GlobalKey(),
    'account': GlobalKey(),
    'status': GlobalKey(),
    'shop': GlobalKey(),
    'faq': GlobalKey(),
    'support': GlobalKey(),
  };

  @override
  void initState() {
    super.initState();
    final hasPrefill = (widget.prefillSubject != null &&
            widget.prefillSubject!.trim().isNotEmpty) ||
        (widget.prefillMessage != null &&
            widget.prefillMessage!.trim().isNotEmpty);
    _showForm = widget.openContactForm || hasPrefill;
    if (widget.prefillSubject != null &&
        widget.prefillSubject!.trim().isNotEmpty) {
      _subjectController.text = widget.prefillSubject!.trim();
    }
    if (widget.prefillMessage != null &&
        widget.prefillMessage!.trim().isNotEmpty) {
      _messageController.text = widget.prefillMessage!.trim();
    }
  }

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _subjectController.dispose();
    _messageController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: 'Aide & Support',
            subtitle: 'Tout ce qu il faut pour commander',
            showBack: true,
            onBack: () => Navigator.of(context).pop(),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DmCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Du Marche a votre Porte',
                            style: Theme.of(context)
                                .textTheme
                                .bodySmall
                                ?.copyWith(color: DmColors.doorOrange)),
                        const SizedBox(height: 6),
                        Text('Aide & Support DoorMarket',
                            style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 6),
                        Text(
                          'Tout ce qu il faut pour commander, payer et suivre vos commandes.',
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ),
                  ),
                  DmCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Container(
                              height: 34,
                              width: 34,
                              decoration: BoxDecoration(
                                color: Theme.of(context).brightness ==
                                        Brightness.dark
                                    ? DmColors.iconBgDark
                                    : DmColors.iconBgLight,
                                borderRadius: DmRadius.r12,
                              ),
                              child: Icon(
                                Icons.menu_book,
                                size: 18,
                                color:
                                    Theme.of(context).brightness == Brightness.dark
                                        ? DmColors.textPrimaryDark
                                        : DmColors.doorBlue,
                              ),
                            ),
                            const SizedBox(width: 10),
                            Text('Sommaire',
                                style:
                                    Theme.of(context).textTheme.headlineSmall),
                          ],
                        ),
                        const SizedBox(height: 12),
                        GridView.count(
                          crossAxisCount: 3,
                          shrinkWrap: true,
                          mainAxisSpacing: 10,
                          crossAxisSpacing: 10,
                          childAspectRatio: 2.3,
                          physics: const NeverScrollableScrollPhysics(),
                          children: [
                            _tocButton(
                                'Presentation', 'intro', Icons.info_outline),
                            _tocButton('Commander', 'how',
                                Icons.shopping_bag_outlined),
                            _tocButton('Produits', 'products',
                                Icons.local_mall_outlined),
                            _tocButton(
                                'Panier', 'cart', Icons.shopping_cart_outlined),
                            _tocButton('Livraison', 'delivery',
                                Icons.local_shipping_outlined),
                            _tocButton('Paiements', 'payments',
                                Icons.payments_outlined),
                            _tocButton(
                                'Compte', 'account', Icons.person_outline),
                            _tocButton(
                                'Statuts', 'status', Icons.track_changes),
                            _tocButton(
                                'Boutique', 'shop', Icons.storefront_outlined),
                            _tocButton('FAQ', 'faq', Icons.quiz_outlined),
                            _tocButton(
                                'Support', 'support', Icons.support_agent),
                          ],
                        ),
                      ],
                    ),
                  ),
                  _sectionCard(
                    key: _sectionKeys['intro']!,
                    title: 'C est quoi DoorMarket ?',
                    body:
                        'DoorMarket vous permet de decouvrir des produits de plusieurs boutiques, d ajouter au panier, de choisir la livraison a votre adresse et de payer en ligne. Apres paiement, la boutique traite la commande et vous suivez son statut jusqu a la livraison.',
                  ),
                  _sectionCard(
                    key: _sectionKeys['how']!,
                    title: 'Comment ca marche',
                    body:
                        'Parcourez les produits (categories, recherche, filtres). Consultez les details (prix, boutique, disponibilite). Ajoutez au panier (quantite + validation). Renseignez l adresse de livraison pour estimer les frais. Finalisez au checkout et payez (Stripe/PayPal/Prepayee). Suivez le statut : Payee -> Traitee -> Livree (ou Annulee).',
                  ),
                  _sectionCard(
                    key: _sectionKeys['products']!,
                    title: 'Produits, boutiques et disponibilite',
                    body:
                        'Les produits proviennent de boutiques partenaires. Les prix et le stock peuvent evoluer. Si un article devient indisponible, la boutique peut ajuster la commande selon les regles metier.',
                  ),
                  _sectionCard(
                    key: _sectionKeys['cart']!,
                    title: 'Panier & commande',
                    body:
                        'Vous pouvez ajouter des articles au panier meme sans compte. Le compte est demande au checkout pour securiser la commande et permettre le suivi.',
                  ),
                  _sectionCard(
                    key: _sectionKeys['delivery']!,
                    title: 'Livraison',
                    body:
                        'Les frais de livraison sont calcules selon votre adresse et l adresse de la boutique. Si votre panier contient plusieurs boutiques, le calcul peut tenir compte de la boutique la plus eloignee. Un petit supplement peut s appliquer.',
                  ),
                  _sectionCard(
                    key: _sectionKeys['payments']!,
                    title: 'Paiements',
                    body:
                        'Paiement par carte (Stripe), PayPal ou carte prepayee selon disponibilite. DoorMarket ne stocke pas les informations sensibles de carte (CVC).',
                  ),
                  _sectionCard(
                    key: _sectionKeys['account']!,
                    title: 'Compte & securite',
                    body:
                        'Votre compte permet de securiser les commandes, sauvegarder vos adresses de livraison, recevoir des notifications et acceder a l historique.',
                  ),
                  _sectionCard(
                    key: _sectionKeys['status']!,
                    title: 'Statuts de commande',
                    body:
                        'En attente -> Payee -> Traitee/En preparation -> En livraison -> Livree. Annulee / Echec en cas de probleme.',
                  ),
                  _sectionCard(
                    key: _sectionKeys['shop']!,
                    title: 'Informations boutique (vendeur)',
                    body:
                        'Lorsqu une commande est payee, la boutique recoit une notification par email "Commande payee en attente". Depuis le Dashboard Boutique, vous pouvez consulter la commande et changer le statut.',
                  ),
                  DmCard(
                    key: _sectionKeys['faq'],
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('FAQ',
                            style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 8),
                        _faqTile('Puis-je voir les produits sans compte ?',
                            'Oui. Le compte est requis au checkout.'),
                        _faqTile('Pourquoi un compte au paiement ?',
                            'Pour securiser la commande et assurer le suivi.'),
                        _faqTile('Comment savoir si le paiement a reussi ?',
                            'La commande apparait comme Payee.'),
                        _faqTile('Comment est calculee la livraison ?',
                            'Selon distance et nombre de boutiques.'),
                        _faqTile('DoorMarket stocke-t-il mes cartes ?',
                            'Non. Stripe/PayPal traitent les paiements.'),
                      ],
                    ),
                  ),
                  DmCard(
                    key: _sectionKeys['support'],
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Support & contact',
                            style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 8),
                        const Text('Email : contact@door-market.com'),
                        const SizedBox(height: 4),
                        const Text('Telephone : (+1) 220-279-1506'),
                        const SizedBox(height: 4),
                        const Text('Horaires : Lun-Sam, 8h-18h'),
                        const SizedBox(height: 12),
                        DmGhostButton(
                          label: _showForm
                              ? 'Masquer le formulaire'
                              : 'Nous contacter',
                          onPressed: () =>
                              setState(() => _showForm = !_showForm),
                        ),
                        const SizedBox(height: 8),
                        DmGhostButton(
                          label: 'Appeler le support',
                          onPressed: _callSupport,
                        ),
                        if (_showForm) ...[
                          const SizedBox(height: 12),
                          _contactForm(),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _tocButton(String label, String target, IconData icon) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight;
    final border = isDark ? DmColors.borderDark : DmColors.borderLight;
    final textColor = DmColors.iconFg(isDark);

    return InkWell(
      onTap: () => _scrollTo(target),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: DmRadius.r14,
          border: Border.all(color: border),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Icon(icon, size: 14, color: DmColors.doorOrange),
            const SizedBox(width: 6),
            Flexible(
              child: Text(
                label,
                style: TextStyle(
                    color: textColor,
                    fontWeight: FontWeight.w600,
                    fontSize: 11),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _sectionCard(
      {required Key key, required String title, required String body}) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: DmCard(
        key: key,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 8),
            Text(body),
          ],
        ),
      ),
    );
  }

  Widget _faqTile(String question, String answer) {
    return ExpansionTile(
      title:
          Text(question, style: const TextStyle(fontWeight: FontWeight.w600)),
      children: [
        Padding(
          padding: const EdgeInsets.only(left: 16, right: 16, bottom: 12),
          child: Text(answer),
        ),
      ],
    );
  }

  Widget _contactForm() {
    return Column(
      children: [
        TextField(
            controller: _nameController,
            decoration: const InputDecoration(labelText: 'Nom')),
        const SizedBox(height: 8),
        TextField(
            controller: _emailController,
            decoration: const InputDecoration(labelText: 'Email')),
        const SizedBox(height: 8),
        TextField(
            controller: _subjectController,
            decoration: const InputDecoration(labelText: 'Sujet')),
        const SizedBox(height: 8),
        TextField(
          controller: _messageController,
          decoration: const InputDecoration(labelText: 'Message'),
          minLines: 4,
          maxLines: 6,
        ),
        const SizedBox(height: 12),
        if (_result != null) Text(_result!),
        DmPrimaryButton(
          label: _loading ? 'Envoi...' : 'Envoyer',
          onPressed: _loading ? null : _send,
        ),
      ],
    );
  }

  Future<void> _send() async {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    final missingMessage =
        isFr ? 'Veuillez remplir tous les champs.' : 'Please fill all fields.';
    setState(() {
      _loading = true;
      _result = null;
    });

    if (_nameController.text.trim().isEmpty ||
        _emailController.text.trim().isEmpty ||
        _subjectController.text.trim().isEmpty ||
        _messageController.text.trim().isEmpty) {
      setState(() => _result = missingMessage);
      await _speak(missingMessage);
      setState(() => _loading = false);
      return;
    }

    try {
      await ref.read(supportApiProvider).sendContact(
            name: _nameController.text.trim(),
            email: _emailController.text.trim(),
            subject: _subjectController.text.trim(),
            message: _messageController.text.trim(),
          );
      final ok = isFr ? 'Message envoye.' : 'Message sent.';
      setState(() => _result = ok);
      await _speak(ok);
    } catch (e) {
      final msg = isFr ? 'Erreur: $e' : 'Error: $e';
      setState(() => _result = msg);
      await _speak(msg);
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _scrollTo(String target) async {
    final key = _sectionKeys[target];
    if (key == null || key.currentContext == null) return;
    await Scrollable.ensureVisible(key.currentContext!,
        duration: const Duration(milliseconds: 350));
  }

  Future<void> _callSupport() async {
    final uri = Uri.parse('tel:+12202791506');
    final opened = await launchUrl(uri);
    if (!opened && mounted) {
      final isFr =
          Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
      final msg = isFr
          ? 'Impossible d ouvrir l application telephone.'
          : 'Unable to open phone application.';
      setState(() => _result = msg);
      await _speak(msg);
    }
  }

  Future<void> _speak(String message) async {
    if (!mounted) return;
    final locale = Localizations.localeOf(context);
    await ref.read(ttsProvider).speak(message, locale);
  }
}

