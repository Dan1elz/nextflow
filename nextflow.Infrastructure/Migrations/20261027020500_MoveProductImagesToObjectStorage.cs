using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nextflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveProductImagesToObjectStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // As imagens saíram do disco (assets/) para o storage S3 e as antigas foram descartadas.
            // Só valem chaves do storage (products/...): o resto (nome de arquivo antigo ou "") vira NULL.
            migrationBuilder.Sql("UPDATE products SET \"Image\" = NULL WHERE \"Image\" IS NOT NULL AND \"Image\" NOT LIKE 'products/%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: os arquivos antigos não existem mais
        }
    }
}
