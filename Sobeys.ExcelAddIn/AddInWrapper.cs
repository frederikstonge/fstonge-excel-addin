﻿using Sobeys.ExcelAddIn.Models;
using Sobeys.ExcelAddIn.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using Excel = Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;

namespace Sobeys.ExcelAddIn
{
    public class AddInWrapper : IAddInWrapper, IDisposable
    {
        private Ribbon _ribbon;
        private CompositionContainer _container;
        private Dictionary<string, WorkbookContainer> _workbookContainers;

        public AddInWrapper(Ribbon ribbon)
        {
            _ribbon = ribbon;
            _container = new CompositionContainer();
            var batch = new CompositionBatch();
            batch.AddExportedValue(_ribbon);
            batch.AddExportedValue(Globals.ThisAddIn);
            batch.AddExportedValue(this);
            _container.Compose(batch);

            _workbookContainers = new Dictionary<string, WorkbookContainer>();

            Globals.ThisAddIn.Application.WorkbookOpen += Application_WorkbookOpen;
            Globals.ThisAddIn.Application.WorkbookBeforeClose += Application_WorkbookBeforeClose;
            Globals.ThisAddIn.Application.WorkbookActivate += Application_WorkbookActivate;
        }

        public void OnWorkbookAction(Office.IRibbonControl control)
        {
            var activeWorkbook = GetActiveWorkbookWrapper();
            if (activeWorkbook != null)
            {
                activeWorkbook.OnAction(control);
            }
        }

        public void OnAction(Office.IRibbonControl control)
        {
            switch (control.Id)
            {
                case RibbonButtons.About:
                    System.Diagnostics.Process.Start("https://github.com/frederikstonge/sobeys-excel-addin");
                    break;
            }
        }

        public bool GetWorkbookEnabled(Office.IRibbonControl control)
        {
            
            var activeWorkbook = GetActiveWorkbookWrapper();
            if (activeWorkbook != null)
            {
                return activeWorkbook.GetEnabled(control);
            }

            return false;
        }

        public bool GetEnabled(Office.IRibbonControl control)
        {

            switch (control.Id)
            {
                case RibbonButtons.About:
                    return true;
                default:
                    return false;
            }
        }

        private WorkbookService GetActiveWorkbookWrapper()
        {
            if (Globals.ThisAddIn.Application.ActiveWorkbook != null)
            {
                return _workbookContainers[Globals.ThisAddIn.Application.ActiveWorkbook.FullName].WorkbookWrapper;
            }

            return null;
        }


        private void Application_WorkbookActivate(Excel.Workbook workbook)
        {
            if (!_workbookContainers.ContainsKey(workbook.FullName))
            {
                AddWorkbook(workbook);
            }

            _ribbon.Invalidate();
        }

        private void Application_WorkbookBeforeClose(Excel.Workbook workbook, ref bool cancel)
        {
            if (!cancel)
            {
                RemoveWorkbook(workbook.FullName);
            }
        }

        private void Application_WorkbookOpen(Excel.Workbook workbook)
        {
            AddWorkbook(workbook);
        }

        private void AddWorkbook(Excel.Workbook workbook)
        {
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new AssemblyCatalog(typeof(AddInWrapper).Assembly));
            var container = new CompositionContainer(catalog);
            var batch = new CompositionBatch();
            batch.AddExportedValue(_ribbon);
            batch.AddExportedValue(Globals.ThisAddIn);
            batch.AddExportedValue(workbook);
            batch.AddExportedValue(this);
            container.Compose(batch);

            var workBookWrapper = container.GetExportedValue<WorkbookService>();
            _workbookContainers.Add(workbook.FullName, new WorkbookContainer(container, workBookWrapper));
        }

        private void RemoveWorkbook(string key)
        {
            var container = _workbookContainers[key];
            container.Container.Dispose();
            _workbookContainers.Remove(key);
        }

        public void Dispose()
        {
            Globals.ThisAddIn.Application.WorkbookOpen -= Application_WorkbookOpen;
            Globals.ThisAddIn.Application.WorkbookBeforeClose -= Application_WorkbookBeforeClose;
            Globals.ThisAddIn.Application.WorkbookActivate -= Application_WorkbookActivate;

            foreach (var workbookContainer in _workbookContainers)
            {
                RemoveWorkbook(workbookContainer.Key);
            }
        }
    }
}
