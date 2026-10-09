import { defineConfig } from '@playwright/test';
import { resolve } from 'node:path';
const root=resolve('../..');
export default defineConfig({
  testDir:'./e2e',workers:1,retries:0,
  use:{baseURL:'https://localhost:4200',ignoreHTTPSErrors:false,trace:'off',screenshot:'off',video:'off'},
  webServer:[
    {command:'dotnet run --no-build --no-launch-profile --project src/backend/Crm.Api',cwd:root,url:'https://localhost:7043/api/ticket-reference-data/categories',reuseExistingServer:false,timeout:120000,ignoreHTTPSErrors:false},
    {command:`npm run start:e2e -- --ssl-cert "${resolve(root,'.local/dev-certs/localhost.pem')}" --ssl-key "${resolve(root,'.local/dev-certs/localhost.key')}"`,cwd:process.cwd(),url:'https://localhost:4200',reuseExistingServer:false,timeout:120000,ignoreHTTPSErrors:false}
  ]
});
