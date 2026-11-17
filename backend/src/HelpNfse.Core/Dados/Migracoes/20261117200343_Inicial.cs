using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HelpNfse.Core.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "simulador_numero_nfse");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "competencias",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    aberta_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competencias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "emissor",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    razao_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    inscricao_municipal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    opcao_simples_nacional = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    regime_apuracao_simples = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    regime_especial_tributacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    serie_dps = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    proximo_numero_dps = table.Column<long>(type: "bigint", nullable: false),
                    regra_data_competencia = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    endereco_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    endereco_codigo_municipio = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    endereco_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    endereco_nome_municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_emissor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recorrencias",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    identificacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false),
                    campos_variaveis = table.Column<int>(type: "integer", nullable: false),
                    referencia_servico = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    desativada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    servico_aliquota_iss = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    servico_classificacao_tributaria_ibs_cbs = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    servico_codigo_municipio_prestacao = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    servico_codigo_nbs = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    servico_codigo_tributacao_municipal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    servico_codigo_tributacao_nacional = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    servico_cst_ibs_cbs = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    servico_descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    servico_indicador_operacao_ibs_cbs = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    servico_informacoes_complementares = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    servico_iss_retido = table.Column<bool>(type: "boolean", nullable: false),
                    servico_tributacao_issqn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tomador_documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    tomador_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tomador_inscricao_municipal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tomador_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tomador_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tomador_endereco_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tomador_endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    tomador_endereco_codigo_municipio = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    tomador_endereco_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tomador_endereco_logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tomador_endereco_nome_municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tomador_endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tomador_endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recorrencias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "simulador_registros",
                columns: table => new
                {
                    identificador_dps = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    cenario = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    emitida = table.Column<bool>(type: "boolean", nullable: false),
                    numero_nfse = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    chave_acesso = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    emitida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    chamadas_envio = table.Column<int>(type: "integer", nullable: false),
                    chamadas_consulta = table.Column<int>(type: "integer", nullable: false),
                    falhas_geradas = table.Column<int>(type: "integer", nullable: false),
                    interrupcao_executada = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_simulador_registros", x => x.identificador_dps);
                });

            migrationBuilder.CreateTable(
                name: "status_workers",
                columns: table => new
                {
                    worker_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_sinal = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    provedor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_status_workers", x => x.worker_id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lotes_emissao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    competencia_id = table.Column<long>(type: "bigint", nullable: false),
                    chave_idempotencia = table.Column<Guid>(type: "uuid", nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    valor_total = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    solicitado_por = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes_emissao", x => x.id);
                    table.ForeignKey(
                        name: "fk_lotes_emissao_competencias_competencia_id",
                        column: x => x.competencia_id,
                        principalTable: "competencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    competencia_id = table.Column<long>(type: "bigint", nullable: false),
                    recorrencia_id = table.Column<long>(type: "bigint", nullable: true),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    identificacao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    campos_variaveis = table.Column<int>(type: "integer", nullable: false),
                    campos_revisados = table.Column<int>(type: "integer", nullable: false),
                    mes_referencia = table.Column<DateOnly>(type: "date", nullable: false),
                    descricao_modelo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    informacoes_complementares_modelo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    valor = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    situacao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    motivo_ignorada = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cenario_simulacao = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    serie_dps = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    numero_dps = table.Column<long>(type: "bigint", nullable: true),
                    identificador_dps = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    data_competencia = table.Column<DateOnly>(type: "date", nullable: true),
                    dps_json = table.Column<string>(type: "jsonb", nullable: true),
                    numero_nfse = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    chave_acesso = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    emitida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    emissao_simulada = table.Column<bool>(type: "boolean", nullable: false),
                    xml_autorizado = table.Column<string>(type: "text", nullable: true),
                    ultima_mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    servico_aliquota_iss = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    servico_classificacao_tributaria_ibs_cbs = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    servico_codigo_municipio_prestacao = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    servico_codigo_nbs = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    servico_codigo_tributacao_municipal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    servico_codigo_tributacao_nacional = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    servico_cst_ibs_cbs = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    servico_descricao = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    servico_indicador_operacao_ibs_cbs = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    servico_informacoes_complementares = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    servico_iss_retido = table.Column<bool>(type: "boolean", nullable: false),
                    servico_tributacao_issqn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tomador_documento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    tomador_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tomador_inscricao_municipal = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tomador_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tomador_telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tomador_endereco_bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tomador_endereco_cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    tomador_endereco_codigo_municipio = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    tomador_endereco_complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tomador_endereco_logradouro = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tomador_endereco_nome_municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tomador_endereco_numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    tomador_endereco_uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notas", x => x.id);
                    table.ForeignKey(
                        name: "fk_notas_competencias_competencia_id",
                        column: x => x.competencia_id,
                        principalTable: "competencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notas_recorrencias_recorrencia_id",
                        column: x => x.recorrencia_id,
                        principalTable: "recorrencias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tentativas_emissao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nota_id = table.Column<long>(type: "bigint", nullable: false),
                    trabalho_id = table.Column<long>(type: "bigint", nullable: true),
                    sequencia = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    resultado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finalizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    identificador_dps = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    provedor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    worker_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    detalhe = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    mensagens = table.Column<string>(type: "jsonb", nullable: false),
                    dados_enviados_json = table.Column<string>(type: "jsonb", nullable: true),
                    numero_nfse = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    chave_acesso = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tentativas_emissao", x => x.id);
                    table.ForeignKey(
                        name: "fk_tentativas_emissao_notas_nota_id",
                        column: x => x.nota_id,
                        principalTable: "notas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trabalhos_emissao",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nota_id = table.Column<long>(type: "bigint", nullable: false),
                    lote_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proxima_execucao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    bloqueado_por = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    token_bloqueio = table.Column<Guid>(type: "uuid", nullable: true),
                    execucoes = table.Column<int>(type: "integer", nullable: false),
                    falhas_temporarias = table.Column<int>(type: "integer", nullable: false),
                    consultas_indisponiveis = table.Column<int>(type: "integer", nullable: false),
                    ultima_mensagem = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabalhos_emissao", x => x.id);
                    table.ForeignKey(
                        name: "fk_trabalhos_emissao_lotes_emissao_lote_id",
                        column: x => x.lote_id,
                        principalTable: "lotes_emissao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trabalhos_emissao_notas_nota_id",
                        column: x => x.nota_id,
                        principalTable: "notas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competencias_ano_mes",
                table: "competencias",
                columns: new[] { "ano", "mes" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lotes_emissao_chave_idempotencia",
                table: "lotes_emissao",
                column: "chave_idempotencia",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lotes_emissao_competencia_id",
                table: "lotes_emissao",
                column: "competencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_notas_competencia_id_recorrencia_id",
                table: "notas",
                columns: new[] { "competencia_id", "recorrencia_id" },
                unique: true,
                filter: "recorrencia_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notas_identificador_dps",
                table: "notas",
                column: "identificador_dps",
                unique: true,
                filter: "identificador_dps IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notas_recorrencia_id",
                table: "notas",
                column: "recorrencia_id");

            migrationBuilder.CreateIndex(
                name: "ix_notas_situacao",
                table: "notas",
                column: "situacao");

            migrationBuilder.CreateIndex(
                name: "ix_recorrencias_identificacao",
                table: "recorrencias",
                column: "identificacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tentativas_emissao_nota_id_sequencia",
                table: "tentativas_emissao",
                columns: new[] { "nota_id", "sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trabalhos_emissao_lote_id",
                table: "trabalhos_emissao",
                column: "lote_id");

            migrationBuilder.CreateIndex(
                name: "ix_trabalhos_emissao_status_proxima_execucao_em",
                table: "trabalhos_emissao",
                columns: new[] { "status", "proxima_execucao_em" });

            migrationBuilder.CreateIndex(
                name: "ux_trabalhos_emissao_nota_ativa",
                table: "trabalhos_emissao",
                column: "nota_id",
                unique: true,
                filter: "status IN ('Pendente', 'EmExecucao', 'Suspenso')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "emissor");

            migrationBuilder.DropTable(
                name: "simulador_registros");

            migrationBuilder.DropTable(
                name: "status_workers");

            migrationBuilder.DropTable(
                name: "tentativas_emissao");

            migrationBuilder.DropTable(
                name: "trabalhos_emissao");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "lotes_emissao");

            migrationBuilder.DropTable(
                name: "notas");

            migrationBuilder.DropTable(
                name: "competencias");

            migrationBuilder.DropTable(
                name: "recorrencias");

            migrationBuilder.DropSequence(
                name: "simulador_numero_nfse");
        }
    }
}
