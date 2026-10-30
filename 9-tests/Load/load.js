import http from 'k6/http';
import { check } from 'k6';

// TE-010/011: stress/soak для XDiary. Запуск:
// k6 run -e XDIARY_E2E_BASE_URL=... -e XDIARY_E2E_TOKEN=... load.js

const BASE = __ENV.XDIARY_E2E_BASE_URL;
const TOKEN = __ENV.XDIARY_E2E_TOKEN;

export const options = {
  scenarios: {
    baseline_1tpm: {
      executor: 'constant-arrival-rate',
      rate: 1,
      timeUnit: '1m',
      duration: '10m',
      preAllocatedVUs: 2,
      maxVUs: 5,
    },
    peak_burst: {
      executor: 'constant-arrival-rate',
      rate: 10,
      timeUnit: '1m',
      duration: '2m',
      startTime: '11m',
      preAllocatedVUs: 5,
      maxVUs: 10,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<2000'],
  },
};

export default function () {
  const res = http.get(`${BASE}/api/v1/journal/records`, {
    headers: { Authorization: `Bearer ${TOKEN}` },
  });
  check(res, { 'status 200': (r) => r.status === 200 });
}
