import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';

const dir = 'c:/Users/Felipe/source/repos/minhas.pocs.gcp/download';
const drawioExe = 'C:/Program Files/draw.io/draw.io.exe';

function esc(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// Build an Entity-Relationship table cell for drawio (mxGraph)
function buildEntity({ id, x, y, name, columns, color }) {
  const stroke = color.stroke;
  const fill = color.fill;
  const headerH = 30;
  const rowH = 24;
  const w = 360;
  const h = headerH + rowH * columns.length;

  const cells = [];

  // Container
  cells.push(`<mxCell id="${id}" value="${esc(name)}" style="shape=table;startSize=${headerH};container=1;collapsible=0;childLayout=tableLayout;fontSize=13;fillColor=${fill};strokeColor=${stroke};fontStyle=1;align=center;verticalAlign=middle;swimlaneFillColor=#ffffff;" vertex="1" parent="1">
    <mxGeometry x="${x}" y="${y}" width="${w}" height="${h}" as="geometry"/>
  </mxCell>`);

  // Rows
  columns.forEach((c, i) => {
    const rowId = `${id}_row_${i}`;
    const cellPkId = `${id}_cell_pk_${i}`;
    const cellNameId = `${id}_cell_name_${i}`;
    const cellTypeId = `${id}_cell_type_${i}`;

    // Row container
    cells.push(`<mxCell id="${rowId}" value="" style="shape=tableRow;horizontal=0;startSize=0;swimlaneHead=0;swimlaneBody=0;strokeColor=inherit;top=0;left=0;bottom=0;right=0;collapsible=0;dropTarget=0;fillColor=none;points=[[0,0.5],[1,0.5]];portConstraint=eastwest;fontSize=11;" vertex="1" parent="${id}">
      <mxGeometry y="${headerH + i * rowH}" width="${w}" height="${rowH}" as="geometry"/>
    </mxCell>`);

    // PK/FK marker column (60px)
    cells.push(`<mxCell id="${cellPkId}" value="${esc(c.key || '')}" style="shape=partialRectangle;html=1;whiteSpace=wrap;connectable=0;strokeColor=inherit;overflow=hidden;fillColor=none;top=0;left=0;bottom=0;right=0;pointerEvents=1;fontSize=10;fontStyle=${c.key ? 1 : 0};align=center;" vertex="1" parent="${rowId}">
      <mxGeometry width="60" height="${rowH}" as="geometry"/>
    </mxCell>`);

    // Column name (180px)
    cells.push(`<mxCell id="${cellNameId}" value="${esc(c.name)}" style="shape=partialRectangle;html=1;whiteSpace=wrap;connectable=0;strokeColor=inherit;overflow=hidden;fillColor=none;top=0;left=0;bottom=0;right=0;pointerEvents=1;fontSize=11;align=left;spacingLeft=4;" vertex="1" parent="${rowId}">
      <mxGeometry x="60" width="180" height="${rowH}" as="geometry"/>
    </mxCell>`);

    // Type (120px)
    cells.push(`<mxCell id="${cellTypeId}" value="${esc(c.type)}" style="shape=partialRectangle;html=1;whiteSpace=wrap;connectable=0;strokeColor=inherit;overflow=hidden;fillColor=none;top=0;left=0;bottom=0;right=0;pointerEvents=1;fontSize=10;align=left;spacingLeft=4;fontColor=#555555;" vertex="1" parent="${rowId}">
      <mxGeometry x="240" width="120" height="${rowH}" as="geometry"/>
    </mxCell>`);
  });

  return { xml: cells.join('\n        '), w, h };
}

// Color palette
const C_BLUE = { fill: '#DAE8FC', stroke: '#6C8EBF' };
const C_GREEN = { fill: '#D5E8D4', stroke: '#82B366' };
const C_ORANGE = { fill: '#FFE6CC', stroke: '#D79B00' };
const C_PURPLE = { fill: '#E1D5E7', stroke: '#9673A6' };
const C_YELLOW = { fill: '#FFF2CC', stroke: '#D6B656' };

// Entities
const entities = [
  {
    id: 'TB_DOC_TIPO',
    x: 40,
    y: 40,
    name: 'TB_DOC_TIPO',
    color: C_BLUE,
    columns: [
      { key: 'PK', name: 'ID', type: 'NUMBER' },
      { key: '', name: 'CODIGO', type: 'VARCHAR2(20)' },
      { key: '', name: 'DESCRICAO', type: 'VARCHAR2(200)' },
      { key: '', name: 'ESCOPO', type: 'VARCHAR2(15)' },
      { key: '', name: 'CODIGO_GED ⚠', type: 'VARCHAR2(50)' },
      { key: '', name: 'DT_CRIACAO', type: 'TIMESTAMP' },
    ],
  },
  {
    id: 'TB_DOC_FUNCIONARIO',
    x: 40,
    y: 280,
    name: 'TB_DOC_FUNCIONARIO',
    color: C_GREEN,
    columns: [
      { key: 'PK', name: 'CPF', type: 'VARCHAR2(11)' },
      { key: '', name: 'EMPRESA_ID', type: 'NUMBER' },
      { key: '', name: 'NOME', type: 'VARCHAR2(200)' },
      { key: '', name: 'MATRICULA', type: 'VARCHAR2(30)' },
      { key: '', name: 'DT_ATUALIZACAO', type: 'TIMESTAMP' },
    ],
  },
  {
    id: 'TB_DOC_SYNC_JOB',
    x: 920,
    y: 40,
    name: 'TB_DOC_SYNC_JOB',
    color: C_PURPLE,
    columns: [
      { key: 'PK', name: 'ID', type: 'NUMBER' },
      { key: '', name: 'DT_INICIO', type: 'TIMESTAMP' },
      { key: '', name: 'DT_FIM', type: 'TIMESTAMP' },
      { key: '', name: 'STATUS', type: 'VARCHAR2(10)' },
      { key: '', name: 'QT_PROCESSADOS', type: 'NUMBER' },
      { key: '', name: 'QT_FALHAS', type: 'NUMBER' },
      { key: '', name: 'DETALHE_ERRO', type: 'CLOB' },
    ],
  },
  {
    id: 'TB_DOC_PREV_CONTROLE',
    x: 480,
    y: 100,
    name: 'TB_DOC_PREV_CONTROLE',
    color: C_ORANGE,
    columns: [
      { key: 'PK', name: 'ID', type: 'NUMBER' },
      { key: '', name: 'EMPRESA_ID', type: 'NUMBER' },
      { key: 'FK', name: 'TIPO_DOC_ID', type: 'NUMBER' },
      { key: 'FK', name: 'FUNCIONARIO_CPF', type: 'VARCHAR2(11)' },
      { key: '', name: 'VERSAO', type: 'NUMBER' },
      { key: '', name: 'ESCOPO_REGISTRO', type: 'VARCHAR2(15)' },
      { key: '', name: 'GCS_BUCKET', type: 'VARCHAR2(100)' },
      { key: '', name: 'GCS_OBJECT_KEY', type: 'VARCHAR2(500)' },
      { key: '', name: 'HASH_SHA256', type: 'VARCHAR2(64)' },
      { key: '', name: 'DT_UPLOAD_SOC', type: 'TIMESTAMP' },
      { key: '', name: 'SYNC_STATUS', type: 'VARCHAR2(10)' },
      { key: 'FK', name: 'SYNC_JOB_ID', type: 'NUMBER' },
    ],
  },
  {
    id: 'TB_DOC_AUDITORIA',
    x: 920,
    y: 380,
    name: 'TB_DOC_AUDITORIA',
    color: C_YELLOW,
    columns: [
      { key: 'PK', name: 'ID', type: 'NUMBER' },
      { key: 'FK', name: 'DOC_PREV_ID', type: 'NUMBER' },
      { key: '', name: 'USUARIO_ID', type: 'NUMBER' },
      { key: '', name: 'EMPRESA_ID', type: 'NUMBER' },
      { key: '', name: 'ACAO', type: 'VARCHAR2(15)' },
      { key: '', name: 'DT_ACAO', type: 'TIMESTAMP' },
      { key: '', name: 'IP_ORIGEM', type: 'VARCHAR2(45)' },
    ],
  },
];

// Build entity XML
const entityCells = entities.map(buildEntity).map((b) => b.xml).join('\n        ');

// Relationships (edges with crow's foot notation)
function rel({ id, source, target, label, sourceMul = 'ERone', targetMul = 'ERmany' }) {
  // sourceMul/targetMul: ERone, ERmany, ERoneToOne, ERoneToMany, ERmandOne, ERzeroToOne, ERzeroToMany
  return `<mxCell id="${id}" value="${esc(label)}" style="edgeStyle=entityRelationEdgeStyle;fontSize=11;html=1;endArrow=${targetMul};startArrow=${sourceMul};rounded=0;exitX=1;exitY=0.5;entryX=0;entryY=0.5;strokeColor=#444444;" edge="1" parent="1" source="${source}" target="${target}">
    <mxGeometry relative="1" as="geometry"/>
  </mxCell>`;
}

const relCells = [
  rel({ id: 'r1', source: 'TB_DOC_TIPO', target: 'TB_DOC_PREV_CONTROLE', label: '1 : N  classifica', sourceMul: 'ERone', targetMul: 'ERmany' }),
  rel({ id: 'r2', source: 'TB_DOC_FUNCIONARIO', target: 'TB_DOC_PREV_CONTROLE', label: '0..1 : N  vincula (CPF)', sourceMul: 'ERzeroToOne', targetMul: 'ERmany' }),
  rel({ id: 'r3', source: 'TB_DOC_PREV_CONTROLE', target: 'TB_DOC_AUDITORIA', label: '1 : N  auditado por', sourceMul: 'ERone', targetMul: 'ERmany' }),
  rel({ id: 'r4', source: 'TB_DOC_SYNC_JOB', target: 'TB_DOC_PREV_CONTROLE', label: '1 : N  sincronizou', sourceMul: 'ERone', targetMul: 'ERmany' }),
].join('\n        ');

// Title cell
const titleCell = `<mxCell id="title" value="Modelo de Dados Oracle — Programa de Prevenção e Controle (P.Ocup)" style="text;html=1;align=center;verticalAlign=middle;fontSize=18;fontStyle=1;fillColor=none;strokeColor=none;" vertex="1" parent="1">
    <mxGeometry x="40" y="0" width="1240" height="36" as="geometry"/>
  </mxCell>`;

// Legend
const legendCell = `<mxCell id="legend" value="&lt;b&gt;Legenda&lt;/b&gt;&lt;br/&gt;PK = Primary Key&lt;br/&gt;FK = Foreign Key&lt;br/&gt;⚠ = Pendência (mapeamento SOC)&lt;br/&gt;&lt;br/&gt;&lt;b&gt;Decisões&lt;/b&gt;&lt;br/&gt;• Histórico imutável: chave única (EMPRESA_ID, TIPO_DOC_ID, FUNCIONARIO_CPF, VERSAO, ESCOPO_REGISTRO)&lt;br/&gt;• FUNCIONARIO_CPF nullable (escopo Empresa)&lt;br/&gt;• SYNC_STATUS: OK | STALE | PENDING&lt;br/&gt;• Binários no GCS, apenas chave no Oracle" style="text;html=1;align=left;verticalAlign=top;whiteSpace=wrap;fillColor=#F5F5F5;strokeColor=#999;fontSize=10;spacingLeft=8;spacingTop=6;" vertex="1" parent="1">
    <mxGeometry x="40" y="500" width="380" height="160" as="geometry"/>
  </mxCell>`;

const xml = `<mxfile host="claude" type="device">
  <diagram id="er" name="ER">
    <mxGraphModel dx="1400" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1400" pageHeight="720" math="0" shadow="0">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        ${titleCell}
        ${entityCells}
        ${relCells}
        ${legendCell}
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>`;

const drawioFile = path.join(dir, 'er-diagram-adr.drawio');
const pngFile = path.join(dir, 'er-diagram-adr.png');

fs.writeFileSync(drawioFile, xml);
console.log('Wrote:', drawioFile);

console.log('Exporting PNG...');
execFileSync(drawioExe, ['-x', '-f', 'png', '-o', pngFile, '--scale', '2', drawioFile], { stdio: 'inherit' });
console.log('Done:', pngFile);
