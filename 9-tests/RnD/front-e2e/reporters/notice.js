// Playwright-reporter: печатает падения как `::error::` — GitHub превращает это
// в аннотацию check-run, которую можно прочитать без токена.
// В сообщение попадают шаги (test.step) — так видно, на каком шаге тест упал/завис.
class NoticeReporter {
  onTestEnd(test, result) {
    if (result.status === test.expectedStatus) return;
    // Длительность каждого шага: после прогона `18 failed ... timedOut` (@DO 2026-11-06 02:34)
    // нужно было видеть, ГДЕ ушло время (`Before Hooks`/вход или сама проверка). Без чисел
    // в логе остаётся только «timedOut», и разбор начинается с догадок.
    const dur = (s) => (typeof s.duration === 'number' && s.duration > 0
      ? ' (' + (Math.round(s.duration / 100) / 10).toFixed(1) + 's)' : '');
    const steps = (result.steps || [])
      .map((s) => s.title + dur(s) + (s.error ? ' [FAIL]' : ''))
      .join(' > ');
    const err = (result.errors || [])
      .map((e) => String((e && (e.message || e.value)) || ''))
      .join(' | ')
      .replace(/[\r\n]+/g, ' ')
      .slice(0, 700);
    console.log('::error::' + JSON.stringify({ title: test.title, status: result.status, steps, error: err }));
  }
  onEnd(result) {
    console.log('::notice::front-e2e: ' + JSON.stringify({ status: result.status }));
  }
  printsToStdio() { return true; }
}

module.exports = NoticeReporter;
