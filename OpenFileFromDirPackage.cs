using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Task = System.Threading.Tasks.Task;
using System.Diagnostics;

namespace OpenFileFromDir
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(OpenFileFromDirPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class OpenFileFromDirPackage : AsyncPackage, IVsSolutionEvents, IVsSolutionEvents7
    {
        public const string PackageGuidString = "d9ceffa2-afa0-4207-8711-d43d74ad1ad8";

        private IVsSolution _solution = null;
        private uint _solutionEventsToken = uint.MaxValue;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await DoOpenFile.InitializeAsync(this);

            HookSolutionEvents();

            // we will be initialized after the user opens a solution
            // so, invoke this here for the first one
            OnSolutionLoaded();
        }

        private void OnSolutionLoaded()
        {
            if (_solution == null) return; // something is wrong here...

            string dir, file, opts;
            _solution.GetSolutionInfo(out dir, out file, out opts);

            Debug.WriteLine("OpenFileFromDirPackage opening {0}", dir);
        }

        private void OnSolutionUnloading()
        {
            Debug.WriteLine("OpenFileFromDirPackage closing solution");
        }

        private void UnhookSolitionEvents()
        {
            if (_solution != null)
            {
                if (_solutionEventsToken != uint.MaxValue)
                {
                    _solution.UnadviseSolutionEvents(_solutionEventsToken);
                    _solutionEventsToken = uint.MaxValue;
                }
                _solution = null;
            }
        }
        private void HookSolutionEvents()
        {
            UnhookSolitionEvents();

            _solution = this.GetService(typeof(SVsSolution)) as IVsSolution;
            if (_solution != null)
            {
                _solution.AdviseSolutionEvents(this, out _solutionEventsToken);
            }
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            OnSolutionLoaded();
            return VSConstants.S_OK;
        }

        public void OnAfterOpenFolder(string folderPath)
        {
            OnSolutionLoaded();
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            // we seem to be getting this event even if a folder is closed
            // that's why we just ignore OnAfterCloseFolder
            OnSolutionUnloading();
            return VSConstants.S_OK;
        }

        #region unused events
        public void OnAfterCloseFolder(string folderPath) { }
        public void OnBeforeCloseFolder(string folderPath) { }
        public void OnQueryCloseFolder(string folderPath, ref int pfCancel) { }
        public void OnAfterLoadAllDeferredProjects() { }

        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded) { return VSConstants.S_OK; }
        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel) { return VSConstants.S_OK; }
        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved) { return VSConstants.S_OK; }
        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy) { return VSConstants.S_OK; }
        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel) { return VSConstants.S_OK; }
        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy) { return VSConstants.S_OK; }
        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel) { return VSConstants.S_OK; }
        public int OnAfterCloseSolution(object pUnkReserved) { return VSConstants.S_OK; }
        public int OnAfterMergeSolution(object pUnkReserved) { return VSConstants.S_OK; }
        #endregion
    }
}
