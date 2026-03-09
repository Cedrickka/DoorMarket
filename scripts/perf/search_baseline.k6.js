import http from 'k6/http';
import { sleep } from 'k6';

export const options = {
  scenarios: {
    search_api_baseline: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '60s', target: 10 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.02'],
    http_req_duration: ['p(50)<350', 'p(95)<900'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

function get(path) {
  const res = http.get(`${BASE_URL}${path}`);
  return res;
}

export default function () {
  get('/api/search/suggestions?q=apple&limit=8');
  get('/api/search/products?q=apple&page=1&pageSize=12&inStockOnly=true&sort=relevance');
  get('/api/search/shops?q=app&page=1&pageSize=12&verifiedOnly=true&sort=relevance');
  get('/api/search/categories?q=appl&limit=24');
  sleep(0.5);
}
