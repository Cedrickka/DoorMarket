import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  scenarios: {
    baseline_api_mix: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '1m', target: 10 },
        { duration: '2m', target: 25 },
        { duration: '2m', target: 40 },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '20s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(50)<350', 'p(95)<900', 'p(99)<1500'],
  },
};

const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5292').replace(/\/+$/, '');

function get(path) {
  const response = http.get(`${BASE_URL}${path}`);
  check(response, {
    'status is 2xx': (r) => r.status >= 200 && r.status < 300,
  });
  return response;
}

export default function () {
  get('/ping');
  get('/api/health/live');
  get('/api/products?page=1&pageSize=12');
  get('/api/shops?page=1&pageSize=12');
  get('/api/search/suggestions?q=door');
  get('/api/search/products?q=door&page=1&pageSize=12&inStockOnly=false&sort=relevance');

  sleep(0.4);
}
