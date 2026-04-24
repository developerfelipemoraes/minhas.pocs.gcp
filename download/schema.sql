-- =====================================================================
-- SD 248562 - Consulta Documentação Técnica (Programa de Prevenção e Controle)
-- Oracle DDL
-- =====================================================================

-- ---------------------------------------------------------------------
-- Tabela: DOC_TIPO
-- Catálogo dos 17 tipos de documentos com escopo (Empresa/Funcionário/Ambos)
-- ---------------------------------------------------------------------
CREATE TABLE DOC_TIPO (
    ID              NUMBER(5)       NOT NULL,
    CODIGO          VARCHAR2(50)    NOT NULL,
    DESCRICAO       VARCHAR2(200)   NOT NULL,
    ESCOPO          VARCHAR2(20)    NOT NULL,  -- EMPRESA | FUNCIONARIO | AMBOS
    ATIVO           NUMBER(1)       DEFAULT 1 NOT NULL,
    DT_CRIACAO      TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
    CONSTRAINT PK_DOC_TIPO PRIMARY KEY (ID),
    CONSTRAINT UK_DOC_TIPO_CODIGO UNIQUE (CODIGO),
    CONSTRAINT CK_DOC_TIPO_ESCOPO CHECK (ESCOPO IN ('EMPRESA','FUNCIONARIO','AMBOS'))
);

-- ---------------------------------------------------------------------
-- Tabela: DOC_PREV_CONTROLE
-- Metadados dos documentos sincronizados do SOC
-- ---------------------------------------------------------------------
CREATE TABLE DOC_PREV_CONTROLE (
    ID                  NUMBER(19)      NOT NULL,
    EMPRESA_ID          NUMBER(19)      NOT NULL,
    FUNCIONARIO_CPF     VARCHAR2(14)    NULL,       -- NULL quando escopo=EMPRESA
    TIPO_DOC_ID         NUMBER(5)       NOT NULL,
    ESCOPO_REGISTRO     VARCHAR2(20)    NOT NULL,   -- EMPRESA | FUNCIONARIO
    VERSAO              NUMBER(5)       NOT NULL,
    NOME_ARQUIVO        VARCHAR2(255)   NOT NULL,
    DT_UPLOAD_SOC       TIMESTAMP       NOT NULL,
    DT_SINCRONIZACAO    TIMESTAMP       NOT NULL,
    GCS_OBJECT_KEY      VARCHAR2(500)   NOT NULL,   -- Chave do objeto no bucket (lida via careplus.infra-service-client)
    GCS_BUCKET          VARCHAR2(100)   NOT NULL,
    HASH_SHA256         VARCHAR2(64)    NOT NULL,
    TAMANHO_BYTES       NUMBER(19)      NOT NULL,
    SOC_DOC_ID          VARCHAR2(100)   NOT NULL,   -- ID original no SOC (rastreabilidade)
    SYNC_STATUS         VARCHAR2(20)    NOT NULL,   -- OK | STALE | PENDING
    ATIVO               NUMBER(1)       DEFAULT 1 NOT NULL,
    CONSTRAINT PK_DOC_PREV_CONTROLE PRIMARY KEY (ID),
    CONSTRAINT FK_DOC_PREV_TIPO FOREIGN KEY (TIPO_DOC_ID) REFERENCES DOC_TIPO(ID),
    CONSTRAINT UK_DOC_PREV_VERSAO UNIQUE (EMPRESA_ID, TIPO_DOC_ID, FUNCIONARIO_CPF, VERSAO, ESCOPO_REGISTRO),
    CONSTRAINT CK_DOC_PREV_ESCOPO CHECK (ESCOPO_REGISTRO IN ('EMPRESA','FUNCIONARIO')),
    CONSTRAINT CK_DOC_PREV_STATUS CHECK (SYNC_STATUS IN ('OK','STALE','PENDING'))
);

CREATE SEQUENCE SEQ_DOC_PREV_CONTROLE START WITH 1 INCREMENT BY 1 NOCACHE;

CREATE INDEX IX_DOC_PREV_EMPRESA          ON DOC_PREV_CONTROLE (EMPRESA_ID, ESCOPO_REGISTRO, TIPO_DOC_ID);
CREATE INDEX IX_DOC_PREV_FUNC             ON DOC_PREV_CONTROLE (EMPRESA_ID, FUNCIONARIO_CPF, TIPO_DOC_ID);
CREATE INDEX IX_DOC_PREV_DT_UPLOAD        ON DOC_PREV_CONTROLE (EMPRESA_ID, TIPO_DOC_ID, DT_UPLOAD_SOC DESC);

-- ---------------------------------------------------------------------
-- Tabela: DOC_FUNCIONARIO
-- Snapshot dos funcionários sincronizados (para filtro por nome/CPF)
-- ---------------------------------------------------------------------
CREATE TABLE DOC_FUNCIONARIO (
    ID                  NUMBER(19)      NOT NULL,
    EMPRESA_ID          NUMBER(19)      NOT NULL,
    CPF                 VARCHAR2(14)    NOT NULL,
    NOME                VARCHAR2(200)   NOT NULL,
    MATRICULA           VARCHAR2(50)    NULL,
    CARGO               VARCHAR2(150)   NULL,
    ATIVO               NUMBER(1)       DEFAULT 1 NOT NULL,
    DT_SINCRONIZACAO    TIMESTAMP       NOT NULL,
    CONSTRAINT PK_DOC_FUNCIONARIO PRIMARY KEY (ID),
    CONSTRAINT UK_DOC_FUNC_CPF UNIQUE (EMPRESA_ID, CPF)
);

CREATE INDEX IX_DOC_FUNC_NOME ON DOC_FUNCIONARIO (EMPRESA_ID, UPPER(NOME));

-- ---------------------------------------------------------------------
-- Tabela: DOC_SYNC_JOB
-- Controle de execução do Worker de sincronização com o SOC
-- ---------------------------------------------------------------------
CREATE TABLE DOC_SYNC_JOB (
    ID                  NUMBER(19)      NOT NULL,
    EMPRESA_ID          NUMBER(19)      NOT NULL,
    DT_INICIO           TIMESTAMP       NOT NULL,
    DT_FIM              TIMESTAMP       NULL,
    STATUS              VARCHAR2(20)    NOT NULL,  -- RUNNING | SUCCESS | FAILED | PARTIAL
    TOTAL_PROCESSADOS   NUMBER(10)      DEFAULT 0 NOT NULL,
    TOTAL_FALHAS        NUMBER(10)      DEFAULT 0 NOT NULL,
    MENSAGEM_ERRO       VARCHAR2(4000)  NULL,
    CONSTRAINT PK_DOC_SYNC_JOB PRIMARY KEY (ID),
    CONSTRAINT CK_DOC_SYNC_STATUS CHECK (STATUS IN ('RUNNING','SUCCESS','FAILED','PARTIAL'))
);

CREATE SEQUENCE SEQ_DOC_SYNC_JOB START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE INDEX IX_DOC_SYNC_EMPRESA ON DOC_SYNC_JOB (EMPRESA_ID, DT_INICIO DESC);

-- ---------------------------------------------------------------------
-- Tabela: DOC_AUDITORIA
-- Log LGPD de acessos (visualizar / download)
-- ---------------------------------------------------------------------
CREATE TABLE DOC_AUDITORIA (
    ID                  NUMBER(19)      NOT NULL,
    DT_EVENTO           TIMESTAMP       DEFAULT SYSTIMESTAMP NOT NULL,
    USUARIO_ID          NUMBER(19)      NOT NULL,
    EMPRESA_ID          NUMBER(19)      NOT NULL,
    DOCUMENTO_ID        NUMBER(19)      NOT NULL,
    ACAO                VARCHAR2(20)    NOT NULL,  -- VISUALIZAR | DOWNLOAD
    IP_ORIGEM           VARCHAR2(45)    NULL,
    USER_AGENT          VARCHAR2(500)   NULL,
    CONSTRAINT PK_DOC_AUDITORIA PRIMARY KEY (ID),
    CONSTRAINT CK_DOC_AUD_ACAO CHECK (ACAO IN ('VISUALIZAR','DOWNLOAD'))
);

CREATE SEQUENCE SEQ_DOC_AUDITORIA START WITH 1 INCREMENT BY 1 NOCACHE;
CREATE INDEX IX_DOC_AUD_DOC ON DOC_AUDITORIA (DOCUMENTO_ID, DT_EVENTO DESC);

-- ---------------------------------------------------------------------
-- Seed: catálogo de tipos de documento (17 tipos conforme regra de negócio)
-- ---------------------------------------------------------------------
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (1,'AEP','Avaliação Ergonômica Preliminar','EMPRESA');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (2,'AET','Análise Ergonômica','EMPRESA');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (3,'DOSIMETRIA_RUIDO','Dosimetria de Ruído','EMPRESA');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (4,'PPP','Perfil Profissiográfico Previdenciário','FUNCIONARIO');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (5,'LAUDO_NR15','Laudo de Insalubridade - NR 15','AMBOS');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (6,'LAUDO_NR16','Laudo de Periculosidade - NR 16','AMBOS');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (7,'LTCAT','Laudo Técnico das Condições do Ambiente de Trabalho','AMBOS');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (8,'NR01_OS','NR-01 Ordem de Serviço','FUNCIONARIO');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (9,'NR05_ATA_CIPA','NR-05 Acompanhamento CIPA - Ata Reunião Mensal','EMPRESA');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (10,'NR05_IMPL_CIPA','NR-05 Implantação CIPA - ATA Instalação e Posse','EMPRESA');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (11,'NR05_MAPA_RISCO','NR-05 Mapa de Risco','EMPRESA');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (12,'NR05_TRN_CIPA','NR-05 Treinamento CIPA - Certificados','AMBOS');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (13,'NR05_DESIGNADO_CARTA','NR-05 Treinamento Designado CIPA - Carta Designação','EMPRESA');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (14,'NR05_TRN_DESIGNADO','NR-05 Treinamento Designado CIPA','AMBOS');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (15,'NR06_EPI','NR-06 Treinamento EPI','AMBOS');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (16,'NR17_ANEXO2','Treinamento Anexo II NR-17 - Certificados','AMBOS');
INSERT INTO DOC_TIPO (ID,CODIGO,DESCRICAO,ESCOPO) VALUES (17,'ERGO_HOME_OFFICE','Treinamento Ergonomia em Home Office','EMPRESA');
COMMIT;
