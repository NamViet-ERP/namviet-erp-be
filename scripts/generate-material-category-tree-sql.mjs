/**
 * Parse CÂY VẬT TƯ (sheet2) → SQL seed for warehouse.material_categories.
 * Source: WareHouse_Management/.agents/cay_vat_tu_full.json
 */
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const jsonPath = join(__dirname, '../../WareHouse_Management/.agents/cay_vat_tu_full.json');

const COLS = [
  { key: 'C', level: 1 },
  { key: 'D', level: 2 },
  { key: 'E', level: 3 },
  { key: 'F', level: 4 },
  { key: 'G', level: 5 },
];

function normalizeName(raw) {
  return raw.replace(/\s+/g, ' ').trim();
}

function stableCode(level, parentCode, name) {
  const payload = `${level}|${parentCode ?? ''}|${normalizeName(name).toLowerCase()}`;
  const hash = createHash('md5').update(payload).digest('hex').slice(0, 12).toUpperCase();
  return `CAT-${hash}`;
}

function sqlEscape(value) {
  return value.replace(/'/g, "''");
}

const data = JSON.parse(readFileSync(jsonPath, 'utf8'));
const rows = data.sheets?.sheet2 ?? [];

/** @type {Map<string, { code: string, name: string, level: number, parentCode: string | null, sortOrder: number }>} */
const nodes = new Map();

const pathNames = [null, null, null, null, null, null]; // index by level
const sortCounters = new Map(); // parentCode -> next sort

function nextSort(parentCode) {
  const key = parentCode ?? '__root__';
  const n = sortCounters.get(key) ?? 0;
  sortCounters.set(key, n + 1);
  return n;
}

function upsertNode(level, name, parentCode) {
  const trimmed = normalizeName(name);
  if (!trimmed) return null;

  const code = stableCode(level, parentCode, trimmed);
  if (!nodes.has(code)) {
    nodes.set(code, {
      code,
      name: trimmed,
      level,
      parentCode: level === 1 ? null : parentCode,
      sortOrder: nextSort(level === 1 ? null : parentCode),
    });
  }
  return code;
}

function resolveInsert(level, pathNames) {
  if (level === 1) return { parentCode: null, level: 1 };
  for (let parentLevel = level - 1; parentLevel >= 1; parentLevel--) {
    const parentCode = pathNames[parentLevel];
    if (parentCode) {
      return { parentCode, level: parentLevel + 1 };
    }
  }
  return null;
}

/** @type {Map<string, string>} code -> column key that created the node */
const pathSourceCol = new Map();

for (const row of rows) {
  const cells = row.cells ?? {};
  if (cells.C === 'Danh mục ') continue;

  for (const { key, level: colLevel } of COLS) {
    const raw = cells[key];
    if (!raw || !String(raw).trim()) continue;

    let resolved = resolveInsert(colLevel, pathNames);
    if (!resolved) {
      console.warn(`Row ${row.row}: skip "${raw}" — missing parent for column ${key}`);
      continue;
    }

    let { parentCode, level } = resolved;

    // Excel sometimes puts "Phân loại" values in column G — treat consecutive G-only rows as siblings under level 3.
    if (
      key === 'G'
      && !cells.F?.trim()
      && level === 5
      && pathNames[4]
      && pathNames[3]
      && pathSourceCol.get(pathNames[4]) === 'G'
    ) {
      parentCode = pathNames[3];
      level = 4;
    }

    for (let l = level + 1; l <= 5; l++) pathNames[l] = null;

    const code = upsertNode(level, String(raw), parentCode);
    if (code) {
      pathNames[level] = code;
      pathSourceCol.set(code, key);
    }
  }
}

const sorted = [...nodes.values()].sort((a, b) => a.level - b.level || a.sortOrder - b.sortOrder);

const valueRows = sorted.map(
  n =>
    `  ('${sqlEscape(n.code)}', '${sqlEscape(n.name)}', ${n.level}, ${n.parentCode ? `'${sqlEscape(n.parentCode)}'` : 'NULL'}, ${n.sortOrder})`,
);

const sql = `-- Auto-generated from cay_vat_tu_full.json (${sorted.length} nodes)
-- Do not edit by hand; regenerate via: node scripts/generate-material-category-tree-sql.mjs

CREATE TEMP TABLE IF NOT EXISTS _material_category_seed (
  "Code" varchar(50) NOT NULL,
  "Name" varchar(255) NOT NULL,
  "Level" integer NOT NULL,
  "ParentCode" varchar(50),
  "SortOrder" integer NOT NULL
);

TRUNCATE _material_category_seed;

INSERT INTO _material_category_seed ("Code", "Name", "Level", "ParentCode", "SortOrder")
VALUES
${valueRows.join(',\n')};

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", NULL, s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
WHERE s."Level" = 1
  AND s."Code" <> 'UNCATEGORIZED'
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 2
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 3
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 4
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 5
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

DROP TABLE IF EXISTS _material_category_seed;
`;

const outPath = join(__dirname, 'seed-material-category-tree.sql');
writeFileSync(outPath, sql, 'utf8');
console.log(`Wrote ${sorted.length} categories → ${outPath}`);

const migrationPath = join(
  __dirname,
  '../src/Modules/Warehouse/NamViet.ERP.Modules.Warehouse.Infrastructure/Persistence/Migrations/20260625140000_SeedMaterialCategoryTree.cs',
);

const sqlBody = sql
  .split('\n')
  .filter(line => !line.startsWith('--'))
  .join('\n')
  .trim()
  .split('\n')
  .map(line => `            ${line}`)
  .join('\n');

const migrationCs = `using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>Seed cây vật tư 5 cấp từ CÂY VẬT TƯ.xlsx (${sorted.length} nodes).</summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20260625140000_SeedMaterialCategoryTree")]
public class SeedMaterialCategoryTree : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
${sqlBody}
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM warehouse.material_categories c
            WHERE c."Code" LIKE 'CAT-%'
              AND NOT EXISTS (
                SELECT 1 FROM warehouse.materials m
                WHERE m."CategoryId" = c."Id" AND m."DeletedAt" IS NULL
              );
            """);
    }
}
`;

writeFileSync(migrationPath, migrationCs, 'utf8');
console.log(`Wrote migration → ${migrationPath}`);
