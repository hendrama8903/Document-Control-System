# DevExtreme bundle

`DMS/wwwroot/js/devextreme/dx.custom.js` contains only the DevExtreme parts the app uses: core, data, dialog, notify and DataGrid.

To use another widget, add it to `dx.custom.config.js`, then run:

    npm install --ignore-scripts
    npm run build

Keep the `devextreme` version equal to the `DevExtreme.AspNet.Core` version in `DMS/DMS.csproj`. `node_modules`, `dx.custom.js` and `dx.custom.debug.js` in this folder are not committed.
