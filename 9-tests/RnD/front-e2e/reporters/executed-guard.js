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
    // Строки в формате list-репортёра: их разбирает `9-tests/scripts/e2e_receipt.py`.
    // Артефакт `test-results/` уезжает из CI, а лог джобы — нет; без этого файла квитанцию
    // нечем накормить, и «числа из прогона» приходилось бы копировать руками.
    this.lines = [];
  }

  _record(test, result) {
    const mark = result.status === 'passed' ? '\u2713'
      : result.status === 'skipped' ? '-'
      : '\u2717';
    const project = (test.parent && typeof test.parent.project === 'function')
      ? (test.parent.project() || {}).name : '';
    const file = (test.location && test.location.file) || (test.titlePath ? test.titlePath()[0] : '?');
    const line = (test.location && test.location.line) || 1;
    const retry = result.retry ? ' (retry #' + result.retry + ')' : '';
    const idx = this.passed + this.failed + this.flaky + this.skipped + 1;
    this.lines.push(
      mark + '  ' + idx + (project ? ' [' + project + ']' : '') + ' \u203a ' + file + ':' + line
      + ' \u203a ' + test.title + retry
    );
  }

  onTestEnd(test, result) {
    this._record(test, result);
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
    // Квитанцию пишем ВСЕГДА, в том числе когда исполненных тестов ноль: именно этот случай
    // и нужно разбирать по артефакту (без файла триаж скажет «не измерено»).
    this._writeReceipt(summary);
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

  _writeReceipt(summary) {
    try {
      const fs = require('fs');
      const path = require('path');
      const dir = path.resolve(__dirname, '..', 'test-results');
      fs.mkdirSync(dir, { recursive: true });
      const body = this.lines.concat(['', 'итог: ' + summary]).join('\n') + '\n';
      fs.writeFileSync(path.join(dir, 'receipt.txt'), body, 'utf8');
    } catch (e) {
      // файл квитанции — удобство, а не условие прогона: не роняем из-за него
      console.log('::warning::front-e2e: не удалось записать test-results/receipt.txt: ' + e.message);
    }
  }

  printsToStdio() { return true; }
}

module.exports = ExecutedGuardReporter;
