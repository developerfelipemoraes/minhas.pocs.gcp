import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';

const dir = 'c:/tmp/sd-248562';
const drawioExe = 'C:/Program Files/draw.io/draw.io.exe';

function esc(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

// Build a mxGraph drawio file for a sequence diagram.
function buildSeq({ title, participants, steps }) {
  const colW = 170;
  const headerY = 60;
  const headerH = 44;
  const startY = headerY + headerH + 20;
  const stepH = 38;
  const leftPad = 40;

  const cells = [];
  let id = 2;

  // Title
  cells.push(`<mxCell id="title" value="${esc(title)}" style="text;html=1;align=center;verticalAlign=middle;fontSize=16;fontStyle=1;" vertex="1" parent="1">
    <mxGeometry x="${leftPad}" y="10" width="${participants.length * colW}" height="30" as="geometry"/>
  </mxCell>`);

  // Participants
  const pX = {};
  participants.forEach((p, i) => {
    const x = leftPad + i * colW + 10;
    pX[p.id] = x + (colW - 20) / 2;
    const fill = p.actor ? '#FFE599' : '#DAE8FC';
    const stroke = p.actor ? '#BF9000' : '#6C8EBF';
    cells.push(`<mxCell id="p_${p.id}" value="${esc(p.label)}" style="rounded=1;whiteSpace=wrap;html=1;fillColor=${fill};strokeColor=${stroke};fontSize=11;fontStyle=1;" vertex="1" parent="1">
      <mxGeometry x="${x}" y="${headerY}" width="${colW - 20}" height="${headerH}" as="geometry"/>
    </mxCell>`);
  });

  // Compute total height first by walking steps
  let y = startY;
  const placed = [];
  for (const s of steps) {
    if (s.type === 'note') {
      placed.push({ ...s, y });
      y += 32;
    } else if (s.type === 'section') {
      placed.push({ ...s, y });
      y += 30;
    } else {
      placed.push({ ...s, y });
      y += stepH;
    }
  }
  const totalH = y + 30;

  // Lifelines
  participants.forEach((p) => {
    const x = pX[p.id];
    cells.push(`<mxCell id="ll_${p.id}" style="endArrow=none;html=1;dashed=1;strokeColor=#888888;" edge="1" parent="1">
      <mxGeometry relative="1" as="geometry">
        <mxPoint x="${x}" y="${headerY + headerH}" as="sourcePoint"/>
        <mxPoint x="${x}" y="${totalH - 20}" as="targetPoint"/>
      </mxGeometry>
    </mxCell>`);
  });

  // Render steps
  for (const s of placed) {
    if (s.type === 'note') {
      const x1 = pX[s.from] - 50;
      const x2 = pX[s.to] + 50;
      cells.push(`<mxCell id="n_${id++}" value="${esc(s.text)}" style="shape=note;whiteSpace=wrap;html=1;fillColor=#FFF2CC;strokeColor=#D6B656;fontSize=10;align=center;" vertex="1" parent="1">
        <mxGeometry x="${Math.min(x1,x2)}" y="${s.y - 4}" width="${Math.abs(x2-x1)}" height="26" as="geometry"/>
      </mxCell>`);
    } else if (s.type === 'section') {
      cells.push(`<mxCell id="sec_${id++}" value="${esc(s.text)}" style="text;html=1;align=center;verticalAlign=middle;fontSize=11;fontStyle=2;fillColor=#E1D5E7;strokeColor=#9673A6;rounded=0;" vertex="1" parent="1">
        <mxGeometry x="${leftPad}" y="${s.y}" width="${participants.length * colW}" height="22" as="geometry"/>
      </mxCell>`);
    } else {
      const x1 = pX[s.from];
      const x2 = pX[s.to];
      const isReturn = s.type === 'return';
      const isSelf = s.from === s.to;
      const style = isSelf
        ? `endArrow=classic;html=1;rounded=0;exitX=1;exitY=0.5;entryX=1;entryY=0.5;curved=1;strokeColor=#444;fontSize=10;`
        : `endArrow=classic;html=1;rounded=0;${isReturn ? 'dashed=1;' : ''}strokeColor=${isReturn?'#666':'#222'};fontSize=10;`;
      const sourcePt = isSelf
        ? `<mxPoint x="${x1}" y="${s.y + 8}" as="sourcePoint"/>`
        : `<mxPoint x="${x1}" y="${s.y + 12}" as="sourcePoint"/>`;
      const targetPt = isSelf
        ? `<mxPoint x="${x1 + 30}" y="${s.y + 22}" as="targetPoint"/>`
        : `<mxPoint x="${x2}" y="${s.y + 12}" as="targetPoint"/>`;
      const num = s.n ? `${s.n}. ` : '';
      cells.push(`<mxCell id="m_${id++}" value="${esc(num + s.label)}" style="${style}" edge="1" parent="1">
        <mxGeometry relative="1" as="geometry">
          ${sourcePt}
          ${targetPt}
        </mxGeometry>
      </mxCell>`);
    }
  }

  const xml = `<mxfile host="claude" type="device">
  <diagram id="seq" name="Sequence">
    <mxGraphModel dx="1200" dy="800" grid="0" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="${leftPad*2 + participants.length*colW}" pageHeight="${totalH}" math="0" shadow="0">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
        ${cells.join('\n        ')}
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>`;
  return xml;
}

// =================== Diagram 1: UI Runtime ===================
const diag1 = {
  title: 'Diagrama de Sequência — Interação de Negócio da Tela (Runtime)',
  participants: [
    { id: 'RH', label: 'Usuário RH/Admin', actor: true },
    { id: 'UI', label: 'Portal Ocupacional UI' },
    { id: 'GW', label: 'ApiGee / IAM' },
    { id: 'BFF', label: 'API BFF' },
    { id: 'CORE', label: 'API Core' },
    { id: 'DB', label: 'Oracle DB' },
    { id: 'INFRA', label: 'careplus.infra-service-client' },
    { id: 'GCS', label: 'GCS Bucket' },
  ],
  steps: (() => {
    let n = 0;
    const M = (from, to, label, type = 'sync') => ({ type, from, to, label, n: ++n });
    return [
      { type: 'section', text: '═══ Carga inicial da tela ═══' },
      M('RH','UI','Acessa "Programas de Prevenção e Controle"'),
      M('UI','GW','GET /categories (JWT + X-Company-ID)'),
      M('GW','GW','Valida JWT e role (RH/ADMIN)'),
      M('GW','BFF','Encaminha requisição'),
      M('BFF','BFF','RbacFilter (RH/ADMIN)'),
      M('BFF','CORE','GET categorias'),
      M('CORE','DB','SELECT em DOC_TIPO'),
      M('DB','CORE','Catálogo de tipos','return'),
      M('CORE','BFF','Cards Empresa/Funcionário','return'),
      M('BFF','UI','200 OK (categorias + sync-status)','return'),
      M('UI','RH','Exibe 2 cards','return'),

      { type: 'section', text: '═══ alt: Documentos da Empresa ═══' },
      M('RH','UI','Seleciona card "Empresa"'),
      M('UI','BFF','GET /company/documents'),
      M('BFF','CORE','Listar escopo EMPRESA'),
      M('CORE','DB','SELECT DOC_PREV_CONTROLE WHERE ESCOPO=EMPRESA'),
      M('DB','CORE','Metadados + SYNC_STATUS','return'),
      M('CORE','BFF','Lista','return'),
      M('BFF','UI','200 OK','return'),
      M('UI','RH','Exibe lista (tipo, data, ação)','return'),

      { type: 'section', text: '═══ else: Documentos do Funcionário ═══' },
      M('RH','UI','Busca por nome/CPF'),
      M('UI','BFF','GET /employees?search='),
      M('BFF','CORE','EmployeeSearchService'),
      M('CORE','DB','SELECT DOC_FUNCIONARIO (IX_DOC_FUNC_NOME)'),
      M('DB','CORE','Funcionários','return'),
      M('CORE','BFF','Lista','return'),
      M('BFF','UI','200 OK','return'),
      M('RH','UI','Seleciona funcionário'),
      M('UI','BFF','GET /employees/{cpf}/documents'),
      M('BFF','CORE','Documentos nominais'),
      M('CORE','DB','SELECT por FUNCIONARIO_CPF'),
      M('DB','CORE','Metadados','return'),
      M('CORE','BFF','Lista','return'),
      M('BFF','UI','200 OK','return'),
      M('UI','RH','Exibe documentos do funcionário','return'),

      { type: 'section', text: '═══ Download do PDF ═══' },
      M('RH','UI','Clica em ⬇ Download'),
      M('UI','BFF','GET /documents/{id}/download'),
      M('BFF','CORE','DocumentDownloadService(id)'),
      M('CORE','DB','SELECT GCS_OBJECT_KEY, GCS_BUCKET'),
      M('DB','CORE','Chave do binário','return'),
      M('CORE','INFRA','getObjectStream(bucket, key)'),
      M('INFRA','GCS','GET object (stream)'),
      M('GCS','INFRA','Bytes do PDF','return'),
      M('INFRA','CORE','InputStream','return'),
      M('CORE','DB','INSERT DOC_AUDITORIA (DOWNLOAD)'),
      M('CORE','BFF','Stream PDF (application/pdf)','return'),
      M('BFF','UI','Stream (proxy)','return'),
      M('UI','RH','Download iniciado','return'),

      { type: 'section', text: '═══ opt: Documento não sincronizado ═══' },
      M('CORE','BFF','SYNC_STATUS = STALE/PENDING','return'),
      M('BFF','UI','Flag de aviso','return'),
      M('UI','RH','"⚠ Documento ainda não sincronizado"','return'),
    ];
  })(),
};

// =================== Diagram 2: Worker Sync ===================
const diag2 = {
  title: 'Diagrama de Sequência — Worker de Sincronização SOC (Batch Diário)',
  participants: [
    { id: 'SCH', label: 'Cloud Scheduler' },
    { id: 'W',   label: 'Worker Sync (Cloud Run Job)' },
    { id: 'SOC', label: 'SOC WebService (GED)' },
    { id: 'INFRA', label: 'careplus.infra-service-client' },
    { id: 'GCS', label: 'GCS Bucket' },
    { id: 'DB',  label: 'Oracle DB' },
    { id: 'OBS', label: 'Observabilidade / Alertas' },
  ],
  steps: (() => {
    let n = 0;
    const M = (from, to, label, type = 'sync') => ({ type, from, to, label, n: ++n });
    return [
      { type: 'section', text: '═══ Início do job ═══' },
      M('SCH','W','Trigger diário (cron)'),
      M('W','DB','INSERT DOC_SYNC_JOB (status=RUNNING)'),
      M('DB','W','JOB_ID','return'),

      { type: 'section', text: '═══ loop: para cada empresa cliente ═══' },
      M('W','SOC','Solicita documentos novos/alterados'),

      { type: 'section', text: '═══ alt: SOC respondeu OK ═══' },
      M('SOC','W','Metadados + referências de binário','return'),
      { type: 'section', text: '── loop: para cada documento ──' },
      M('W','SOC','Download do binário'),
      M('SOC','W','Bytes PDF','return'),
      M('W','INFRA','putObject(bucket, key, bytes)'),
      M('INFRA','GCS','PUT object'),
      M('GCS','INFRA','ETag/version','return'),
      M('INFRA','W','OK','return'),
      M('W','DB','UPSERT DOC_PREV_CONTROLE (versão, GCS_KEY, hash, OK)'),
      M('W','DB','UPSERT DOC_FUNCIONARIO (snapshot)'),

      { type: 'section', text: '═══ else: Falha do SOC ═══' },
      M('W','W','Retry exponencial'),
      M('W','DB','UPDATE DOC_PREV_CONTROLE SET SYNC_STATUS=STALE'),
      M('W','OBS','Alerta "SOC indisponível"'),
      { type: 'note', from: 'W', to: 'DB', text: 'Snapshot anterior permanece servível ao Portal' },

      { type: 'section', text: '═══ Finalização do job ═══' },
      M('W','DB','UPDATE DOC_SYNC_JOB (OK | PARTIAL | FAILED)'),
      M('W','OBS','Métricas (duração, processados, falhas)'),
      M('W','OBS','Alerta se PARTIAL/FAILED'),
    ];
  })(),
};

// Renumber both (notes/sections have no n)
function renumber(d) {
  let n = 0;
  for (const s of d.steps) if (s.type === 'sync' || s.type === 'return') s.n = ++n;
}
renumber(diag1);
renumber(diag2);

const file1 = path.join(dir, 'sequence-ui.drawio');
const file2 = path.join(dir, 'sequence-worker.drawio');
fs.writeFileSync(file1, buildSeq(diag1));
fs.writeFileSync(file2, buildSeq(diag2));
console.log('Wrote:', file1, file2);

// Export PNGs
for (const f of [file1, file2]) {
  const out = f.replace(/\.drawio$/, '.png');
  console.log('Exporting', out);
  execFileSync(drawioExe, ['-x', '-f', 'png', '-o', out, '--scale', '2', f], { stdio: 'inherit' });
}
console.log('Done');
