// Playwright-reporter: «прогон без шагов — не прогон» (AGENTS §5).
//
// Зачем: если на стенде не выполнено условие (нет флага, нет PIN, стенд отвечает не тем),
// спеки могут уйти в `test.skip()`, и Playwright завершится с кодом 0: «0 passed, N skipped».
// Для CI это выглядит как зелёный прогон, хотя не проверено НИЧЕГО. Ровно этот класс ошибок
// уже случался в проекте («зелёный CI при 0 проектов/0 тестов = не доказательство»).
//
// Правило простое и однозначное: если исполненных тестов (passed + failed + flaky) НОЛЬ,
// репортёр печатает `::error::` и выставляет код возврата 1. Во всех остальных случаях
// он ничего не делает и не влияет на результат прогона.
class ExecutedGuardReporter {
  constructor() {
    this.passed = 0;
    this.failed = 0;
    this.flaky = 0;
    this.skipped = 0;
  }

  onTestEnd(test, result) {
    switch (result.status) {
      case 'passed':
        // ретрай после падения — это flaky, а не чистое прохождение
        if (result.retry > 0) this.flaky += 1;
        else this.passed += 1;
        break;
      case 'failed':
      case 'timedOut':
        this.failed += 1;
        break;
      case 'skipped':
        this.skipped += 1;
        break;
      default:
        break;
    }
  }

  onEnd(result) {
    const executed = this.passed + this.failed + this.flaky;
    const summary = `passed ${this.passed}, failed ${this.failed}, flaky ${this.flaky}, skipped ${this.skipped}`;
    if (executed === 0) {
      // Не бросаем исключение (оно испортит понятный отчёт), а печатаем аннотацию и код возврата.
      console.log('::error::front-e2e: ни один тест не исполнялся (' + summary + ') — '
        + 'набор ничего не проверил, это не зелёный прогон');
      process.exitCode = 1;
      return;
    }
    if (this.skipped > 0) {
      console.log('::notice::front-e2e: исполнено ' + executed + ', пропущено ' + this.skipped
        + ' (' + summary + ', статус ' + result.status + ')');
    }
  }

  printsToStdio() { return true; }
}

module.exports = ExecutedGuardReporter;
