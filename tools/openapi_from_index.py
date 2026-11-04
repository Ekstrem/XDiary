"""Генерация OpenAPI-спек из канонического индекса операций.

Контрактный реестр (не финальный из кода): источник — 04-architecture/api-index.yaml,
80 операций с operationId. Нужен, чтобы APIM импортировал систему целиком (DO-056).

Запуск: python tools/openapi_from_index.py [--check]
"""
import argparse
import datetime as dt
import json
import pathlib
import sys

import yaml

ROOT = pathlib.Path(__file__).resolve().parents[1]
INDEX = ROOT / '04-architecture' / 'api-index.yaml'
OUT = ROOT / 'openapi'

TITLES = {
    'Analytics': 'Analytics Microservice API',
    'ApiGateway': 'API Gateway',
    'AppleHealthGateway': 'Apple Health Gateway API',
    'Catalog': 'Catalog Microservice API',
    'Client': 'Client API',
    'DailyMetrics': 'Daily Metrics Microservice API',
    'GarminGateway': 'Garmin Gateway API',
    'Journal': 'Journal Microservice API',
    'Partners': 'Partners Microservice API',
    'Portability': 'Portability Microservice API',
    'Privacy': 'Privacy Microservice API',
    'SomaticSeries': 'Somatic Series Microservice API',
}

SLUG = {
    'Analytics': 'analytics',
    'ApiGateway': 'api-gateway',
    'AppleHealthGateway': 'apple-health-gateway',
    'Catalog': 'catalog',
    'Client': 'client',
    'DailyMetrics': 'daily-metrics',
    'GarminGateway': 'garmin-gateway',
    'Journal': 'journal',
    'Partners': 'partners',
    'Portability': 'portability',
    'Privacy': 'privacy',
    'SomaticSeries': 'somatic-series',
}


def security_for(auth):
    if auth == 'jwt':
        return [{'bearerAuth': []}]
    return []


def param_names(path):
    """Имена path-параметров из шаблона: /clients/{id}/x -> [id]."""
    names = []
    part = ''
    inside = False
    for ch in path:
        if ch == '{':
            inside = True
            part = ''
        elif ch == '}':
            inside = False
            names.append(part)
        elif inside:
            part += ch
    return names


def responses_for(codes):
    out = {}
    for code, desc in (codes or {}).items():
        out[str(code)] = {'description': str(desc)}
    return out or {'200': {'description': 'ok'}}


def spec_for(bc, ops, meta):
    paths = {}
    tags = set()
    for o in ops:
        p = o['path']
        method = o['method'].lower()
        tags.add(o['operationId'].split('.')[0])
        item = paths.setdefault(p, {})
        params = [
            {'name': n, 'in': 'path', 'required': True, 'schema': {'type': 'string'}}
            for n in param_names(p)
        ]
        op = {
            'operationId': o['operationId'],
            'tags': [o['operationId'].split('.')[0]],
            'responses': responses_for(o.get('codes')),
        }
        if params:
            op['parameters'] = params
        sec = security_for(o.get('auth'))
        if sec:
            op['security'] = sec
        else:
            op['security'] = []
        item[method] = op

    return {
        'openapi': '3.0.3',
        'info': {
            'title': TITLES.get(bc, bc),
            'version': meta.get('date', ''),
            'description': (
                'Контрактный реестр, сгенерированный из 04-architecture/api-index.yaml '
                '(operationId — единственный идентификатор операции). '
                'Финальный реестр из кода — BE-050.'
            ),
        },
        'servers': [{'url': '/'}],
        'x-derived-from': '04-architecture/api-index.yaml',
        'x-generated-at': meta.get('date', ''),
        'x-operations': len(ops),
        'components': {
            'securitySchemes': {
                'bearerAuth': {'type': 'http', 'scheme': 'bearer', 'bearerFormat': 'JWT'}
            }
        },
        'paths': paths,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true', help='только проверить, не писать')
    a = ap.parse_args()

    data = yaml.safe_load(INDEX.read_text(encoding='utf-8'))
    ops = data['operations']
    by_bc = {}
    for o in ops:
        by_bc.setdefault(o['bc'], []).append(o)

    meta = {'date': dt.date.today().isoformat()}
    total_paths = 0
    written = []
    for bc, items in sorted(by_bc.items()):
        slug = SLUG.get(bc)
        if not slug:
            print(f'!! нет slug для BC {bc}', file=sys.stderr)
            return 2
        spec = spec_for(bc, items, meta)
        total_paths += len(spec['paths'])
        p = OUT / f'{slug}.json'
        text = json.dumps(spec, ensure_ascii=False, indent=2) + '\n'
        if a.check:
            old = p.read_text(encoding='utf-8') if p.exists() else ''
            print(f'{slug}: {"OK" if old == text else "ОТЛИЧАЕТСЯ"} ({len(spec["paths"])} путей)')
        else:
            p.write_text(text, encoding='utf-8')
            written.append((slug, len(spec['paths'])))

    if not a.check:
        # _meta.json: rev = хеш содержимого всех спек, чтобы реестр был адресуемым
        import hashlib
        h = hashlib.sha256()
        for slug, _ in sorted(written):
            h.update((OUT / f'{slug}.json').read_bytes())
        meta_out = {}
        for bc, items in sorted(by_bc.items()):
            slug = SLUG[bc]
            meta_out[slug] = {
                'rev': h.hexdigest(),
                'paths': len({o['path'] for o in items}),
                'title': TITLES.get(bc, bc),
                'operations': len(items),
                'derived_from': '04-architecture/api-index.yaml',
            }
        (OUT / '_meta.json').write_text(
            json.dumps(meta_out, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
        print(f'записано спек: {len(written)}, путей всего: {total_paths}, rev: {h.hexdigest()[:12]}')
        for slug, n in written:
            print(f'  {slug}: {n} путей')
    return 0


if __name__ == '__main__':
    sys.exit(main())
