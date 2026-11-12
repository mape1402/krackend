# Runtime detail code viewer

The instance detail page ships a local, read-only CodeMirror bundle. Hosts do
not need Node.js or a CDN to serve it. To update the bundle, run `npm ci`,
`npm test`, and `npm run build` in this directory and commit the generated
`wwwroot/js/detail-code-viewer.js` with the lockfile. Third-party license texts
for the bundled dependencies are in `wwwroot/js/detail-code-viewer.LICENSE.txt`.
