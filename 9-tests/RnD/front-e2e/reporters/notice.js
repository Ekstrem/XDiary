// Playwright-reporter: печатает падения как `::error::` — GitHub превращает это
// в аннотацию check-run, которую можно прочитать без токена.
class NoticeReporter {
  onTestEnd(test, result) {
    if (result.status === test.expectedStatus) return;
    const err = (result.errors || [])
      .map((e) => String((e && (e.message || e.value)) || ''))
      .join(' | ')
      .replace(/[\r\n]+/g, ' ')
      .slice(0, 900);
    console.log('::error::' + JSON.stringify({ title: test.title, status: result.status, error: err }));
  }
  onEnd(result) {
    console.log('::notice::front-e2e: ' + JSON.stringify({ status: result.status }));
  }
  printsToStdio() { return true; }
}

module.exports = NoticeReporter;
