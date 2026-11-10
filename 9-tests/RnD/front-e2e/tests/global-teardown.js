// Глобальный teardown прогона (TE-118).
//
// Зачем: `seedSessionOnce` создаёт партнёра `TE-SEED-<ts>` один раз на процесс и раньше не убирал его.
// Шаг очистки в CI (`DO-044`) удаляет клиентов по тестовым телефонам, а партнёр остаётся — и следующий
// `add-partner` видит в списке чужой сид (`@DO 2026-11-10 16:21`). Teardown удаляет ровно того партнёра,
// которого создал сидер (id взят из маркера), и **никогда не валит прогон**: уборка — не проверка.
const fs = require('fs');
const os = require('os');
const path = require('path');
const { API_BASE } = require('./api');

module.exports = async function globalTeardown() {
  const marker = path.join(os.tmpdir(),
    'xdiary-e2e-seed-' + API_BASE.replace(/[^a-z0-9]+/gi, '_') + '.json');
  let seed = null;
  try {
    seed = JSON.parse(fs.readFileSync(marker, 'utf8'));
  } catch (e) {
    return; // сида не было — убирать нечего
  }
  try {
    if (seed && seed.partnerId && seed.token) {
      const res = await fetch(seed.base + '/api/v1/partners/' + seed.partnerId + '/remove', {
        method: 'POST',
        headers: { Authorization: 'Bearer ' + seed.token, 'Content-Type': 'application/json' },
      });
      console.log('::notice::teardown: сид-партнёр ' + (seed.partnerName || seed.partnerId) +
        ' удалён → ' + res.status);
    }
  } catch (e) {
    console.log('::warning::teardown: сид-партнёр не удалён (' + e.message + ') — это уборка, не проверка');
  } finally {
    try { fs.unlinkSync(marker); } catch (e) { /* уже нет — и хорошо */ }
  }
};
