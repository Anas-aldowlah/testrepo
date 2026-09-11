import { execSync } from 'child_process';
import fs from 'fs';

const TOKEN = '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';
const DATABASE_ID = '3d5d7cbe-e9f0-8131-a474-d6587803f595';

const phasePages = {
  1: '3d5d7cbe-e9f0-8111-9ece-cf09ab89986f', // Security
  2: '3d5d7cbe-e9f0-81c2-b8c6-ec681d6193f2', // Database Region
  3: '3d5d7cbe-e9f0-81b9-a58c-c890b376033c', // Persistent Storage
  4: '3d5d7cbe-e9f0-814c-8a37-c9b50a8aff96', // Caching
  5: '3d5d7cbe-e9f0-81e2-bd36-cb8d6c2d6f44', // Images
  6: '3d5d7cbe-e9f0-813f-bdfa-d830b56b1fe0', // Backend
  7: '3d5d7cbe-e9f0-81c1-9031-c4f42f7ce955', // Database
  8: '3d5d7cbe-e9f0-8167-a25e-e09c80d463b3'  // Frontend
};

export function updatePhase(phaseNumber, status, isDone) {
  const pageId = phasePages[phaseNumber];
  if (!pageId) throw new Error(`Unknown phase number: ${phaseNumber}`);

  const tmpFile = 'C:\\Users\\HP\\AppData\\Local\\Temp\\notion_update_payload.json';
  const payload = {
    properties: {
      "الحالة": { status: { name: status } },
      "تم الإنجاز": { checkbox: isDone }
    }
  };

  fs.writeFileSync(tmpFile, JSON.stringify(payload), 'utf8');
  const cmd = `curl.exe -s -X PATCH -H "Authorization: Bearer ${TOKEN}" -H "Notion-Version: 2022-06-28" -H "Content-Type: application/json" -d "@${tmpFile}" "https://api.notion.com/v1/pages/${pageId}"`;
  const res = execSync(cmd, { encoding: 'utf8' });
  try { if (fs.existsSync(tmpFile)) fs.unlinkSync(tmpFile); } catch(e) {}
  return JSON.parse(res);
}

// CLI test: node update_notion_phase.mjs <phase> <status> <done>
if (process.argv[2]) {
  const phaseNum = parseInt(process.argv[2], 10);
  const status = process.argv[3] || 'جاري التنفيذ';
  const isDone = process.argv[4] === 'true';
  console.log(`Updating Phase ${phaseNum} to '${status}' (done: ${isDone})...`);
  const res = updatePhase(phaseNum, status, isDone);
  console.log('Update successful! Result:', res.id);
}
