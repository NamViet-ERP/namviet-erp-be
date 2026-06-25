/**
 * Generate scripts/seed-material-categories.sql from material-categories-tree.json
 * Run: node scripts/generate-material-categories-sql.mjs
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const jsonPath = path.resolve(
  __dirname,
  '../src/Modules/Warehouse/NamViet.ERP.Modules.Warehouse.Infrastructure/Data/material-categories-tree.json',
);
const outPath = path.resolve(__dirname, 'seed-material-categories.sql');
const repairPath = path.resolve(__dirname, 'repair-material-categories-names.sql');

const items = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
const allItems = [
  {
    code: 'UNCATEGORIZED',
    name: 'Chưa phân loại',
    level: 1,
    parentCode: null,
    sortOrder: 0,
    isActive: true,
  },
  ...items,
];

function esc(value) {
  return String(value).replace(/'/g, "''");
}

const lines = [
  '-- Material category tree (master data) — idempotent',
  '-- Regenerate: node scripts/generate-material-categories-sql.mjs',
  'BEGIN;',
  '',
  `INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'UNCATEGORIZED', 'Chưa phân loại', 1, NULL, 0, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'UNCATEGORIZED'
);`,
  '',
];

for (let level = 1; level <= 5; level += 1) {
  const levelItems = items
    .filter(i => i.level === level)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.name.localeCompare(b.name, 'vi'));

  if (levelItems.length === 0) continue;

  lines.push(`-- Level ${level}`);
  for (const item of levelItems) {
    const code = esc(item.code);
    const name = esc(item.name);
    const active = item.isActive ? 'TRUE' : 'FALSE';

    if (item.parentCode) {
      const parentCode = esc(item.parentCode);
      lines.push(`INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT '${code}', '${name}', ${item.level}, p."Id", ${item.sortOrder}, ${active}, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = '${parentCode}'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = '${code}'
  );`);
    } else {
      lines.push(`INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT '${code}', '${name}', ${item.level}, NULL, ${item.sortOrder}, ${active}, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = '${code}'
);`);
    }
    lines.push('');
  }
}

lines.push('COMMIT;', '');

fs.writeFileSync(outPath, lines.join('\n'), 'utf8');

const repairLines = [
  '-- Repair Vietnamese names (UTF-8) by Code — safe when rows already exist',
  '-- Regenerate: node scripts/generate-material-categories-sql.mjs',
  'BEGIN;',
  '',
  ...allItems.map(item => {
    const code = esc(item.code);
    const name = esc(item.name);
    return `UPDATE warehouse.material_categories SET "Name" = '${name}', "UpdatedAt" = NOW() WHERE "Code" = '${code}';`;
  }),
  '',
  'COMMIT;',
  '',
];

fs.writeFileSync(repairPath, repairLines.join('\n'), 'utf8');
console.log(`Wrote ${outPath} (${items.length} categories)`);
console.log(`Wrote ${repairPath} (${allItems.length} updates)`);
