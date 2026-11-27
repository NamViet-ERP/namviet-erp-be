using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261127140000_CascadeMaterialCodeUpdates")]
public partial class CascadeMaterialCodeUpdates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                rec RECORD;
                del_action text;
            BEGIN
                FOR rec IN
                    SELECT
                        n.nspname AS schema_name,
                        t.relname AS table_name,
                        c.conname AS constraint_name,
                        c.confdeltype::text AS del_type,
                        (
                            SELECT string_agg(quote_ident(a.attname), ', ' ORDER BY k.ord)
                            FROM unnest(c.conkey) WITH ORDINALITY AS k(attnum, ord)
                            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum AND NOT a.attisdropped
                        ) AS local_cols,
                        (
                            SELECT string_agg(quote_ident(fa.attname), ', ' ORDER BY fk.ord)
                            FROM unnest(c.confkey) WITH ORDINALITY AS fk(attnum, ord)
                            JOIN pg_attribute fa ON fa.attrelid = c.confrelid AND fa.attnum = fk.attnum AND NOT fa.attisdropped
                        ) AS ref_cols
                    FROM pg_constraint c
                    JOIN pg_class t ON t.oid = c.conrelid
                    JOIN pg_namespace n ON n.oid = t.relnamespace
                    WHERE c.contype = 'f'
                      AND c.confrelid = 'warehouse.materials'::regclass
                      AND c.confupdtype <> 'c'
                    ORDER BY n.nspname, t.relname, c.conname
                LOOP
                    del_action := CASE rec.del_type
                        WHEN 'a' THEN 'NO ACTION'
                        WHEN 'r' THEN 'RESTRICT'
                        WHEN 'c' THEN 'CASCADE'
                        WHEN 'n' THEN 'SET NULL'
                        WHEN 'd' THEN 'SET DEFAULT'
                        ELSE 'NO ACTION'
                    END;

                    EXECUTE format(
                        'ALTER TABLE %I.%I DROP CONSTRAINT %I',
                        rec.schema_name, rec.table_name, rec.constraint_name
                    );

                    EXECUTE format(
                        'ALTER TABLE %I.%I ADD CONSTRAINT %I FOREIGN KEY (%s) REFERENCES warehouse.materials (%s) ON UPDATE CASCADE ON DELETE %s',
                        rec.schema_name, rec.table_name, rec.constraint_name,
                        rec.local_cols, rec.ref_cols, del_action
                    );
                END LOOP;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                rec RECORD;
                del_action text;
            BEGIN
                FOR rec IN
                    SELECT
                        n.nspname AS schema_name,
                        t.relname AS table_name,
                        c.conname AS constraint_name,
                        c.confdeltype::text AS del_type,
                        (
                            SELECT string_agg(quote_ident(a.attname), ', ' ORDER BY k.ord)
                            FROM unnest(c.conkey) WITH ORDINALITY AS k(attnum, ord)
                            JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = k.attnum AND NOT a.attisdropped
                        ) AS local_cols,
                        (
                            SELECT string_agg(quote_ident(fa.attname), ', ' ORDER BY fk.ord)
                            FROM unnest(c.confkey) WITH ORDINALITY AS fk(attnum, ord)
                            JOIN pg_attribute fa ON fa.attrelid = c.confrelid AND fa.attnum = fk.attnum AND NOT fa.attisdropped
                        ) AS ref_cols
                    FROM pg_constraint c
                    JOIN pg_class t ON t.oid = c.conrelid
                    JOIN pg_namespace n ON n.oid = t.relnamespace
                    WHERE c.contype = 'f'
                      AND c.confrelid = 'warehouse.materials'::regclass
                      AND c.confupdtype = 'c'
                    ORDER BY n.nspname, t.relname, c.conname
                LOOP
                    del_action := CASE rec.del_type
                        WHEN 'a' THEN 'NO ACTION'
                        WHEN 'r' THEN 'RESTRICT'
                        WHEN 'c' THEN 'CASCADE'
                        WHEN 'n' THEN 'SET NULL'
                        WHEN 'd' THEN 'SET DEFAULT'
                        ELSE 'NO ACTION'
                    END;

                    EXECUTE format(
                        'ALTER TABLE %I.%I DROP CONSTRAINT %I',
                        rec.schema_name, rec.table_name, rec.constraint_name
                    );

                    EXECUTE format(
                        'ALTER TABLE %I.%I ADD CONSTRAINT %I FOREIGN KEY (%s) REFERENCES warehouse.materials (%s) ON UPDATE NO ACTION ON DELETE %s',
                        rec.schema_name, rec.table_name, rec.constraint_name,
                        rec.local_cols, rec.ref_cols, del_action
                    );
                END LOOP;
            END $$;
            """);
    }
}
