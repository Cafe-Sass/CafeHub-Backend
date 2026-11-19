using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeTask47ProductTagIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE "__EFMigrationsHistory" IN ACCESS EXCLUSIVE MODE;
                LOCK TABLE product_tags IN ACCESS EXCLUSIVE MODE;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM product_tags
                               WHERE id IS NULL OR id = '00000000-0000-0000-0000-000000000000'
                                  OR tag <> btrim(tag) OR btrim(tag) = '') THEN
                        RAISE EXCEPTION 'Task 47 tag identity backfill and normalization must be verified before finalization';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropPrimaryKey(
                name: "pk_product_tags",
                table: "product_tags");

            migrationBuilder.DropIndex(
                name: "ix_product_tags_tag",
                table: "product_tags");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "product_tags",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_product_tags",
                table: "product_tags",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_tags_normalized_label",
                table: "product_tags",
                sql: "tag = btrim(tag) AND tag <> ''");

            migrationBuilder.CreateIndex(
                name: "ix_product_tags_active_tag",
                table: "product_tags",
                columns: new[] { "tag", "product_id" },
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_product_tags_product_id",
                table: "product_tags",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "uq_product_tags_active_label",
                table: "product_tags",
                columns: new[] { "product_id", "tag" },
                unique: true,
                filter: "status = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE product_tags, product_routing_readiness IN ACCESS EXCLUSIVE MODE;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM product_routing_readiness)
                        OR EXISTS (SELECT 1 FROM product_tags GROUP BY product_id, tag HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Task 47 tag history or cutover state prevents a lossless downgrade';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropPrimaryKey(
                name: "pk_product_tags",
                table: "product_tags");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_tags_normalized_label",
                table: "product_tags");

            migrationBuilder.DropIndex(
                name: "ix_product_tags_active_tag",
                table: "product_tags");

            migrationBuilder.DropIndex(
                name: "ix_product_tags_product_id",
                table: "product_tags");

            migrationBuilder.DropIndex(
                name: "uq_product_tags_active_label",
                table: "product_tags");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "product_tags",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddPrimaryKey(
                name: "pk_product_tags",
                table: "product_tags",
                columns: new[] { "product_id", "tag" });

            migrationBuilder.CreateIndex(
                name: "ix_product_tags_tag",
                table: "product_tags",
                column: "tag");
        }
    }
}
