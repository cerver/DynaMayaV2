using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Dynamo.Interfaces;
using Dynamo.Models;
using Greg;
using Dynamo.UpdateManager;
using Dynamo.Core.Threading;


namespace Dynamo.Applications.Models
{
    public class DynaMayaModel : DynamoModel
    {
        public interface IDMStartConfiguration : IStartConfiguration
        {
           
        }

        public struct DMStartConfiguration 
        {
            public string Context { get; set; }
            public string DynamoCorePath { get; set; }
            public IPathResolver PathResolver { get; set; }
            public IPreferences Preferences { get; set; }
            public bool StartInTestMode { get; set; }
            public IUpdateManager UpdateManager { get; set; }
            public ISchedulerThread SchedulerThread { get; set; }
            public string GeometryFactoryPath { get; set; }
            public IAuthProvider AuthProvider { get; set; }
            public string PackageManagerAddress { get; set; }
            public IEnumerable<Extensions.IExtension> Extensions { get; set; }
            public TaskProcessMode ProcessMode { get; set; }
        }

  
        #region Events

        /// <summary>
        /// Event triggered when the current DM document is changed.
        /// </summary>
        public event EventHandler DMDocumentChanged;

        public virtual void OnDMDocumentChanged()
        {
            if (DMDocumentChanged != null)
                DMDocumentChanged(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event triggered when the DM document that Dynamo had 
        /// previously been pointing at has been closed.
        /// </summary>
        public event Action DMDocumentLost;

        private void OnDMDocumentLost()
        {
            var handler = DMDocumentLost;
            if (handler != null) handler();
        }

        /// <summary>
        /// Event triggered when DM enters a context 
        /// where external applications are not allowed.
        /// </summary>
        public event Action DMContextUnavailable;

        private void OnDMContextUnavailable()
        {
            var handler = DMContextUnavailable;
            if (handler != null) handler();
        }

        /// <summary>
        /// Event triggered when DM enters a context where
        /// external applications are allowed.
        /// </summary>
        public event Action DMContextAvailable;

        private void OnDMContextAvailable()
        {
            var handler = DMContextAvailable;
            if (handler != null) handler();
        }

        /// <summary>
        /// Event triggered when the active DM view changes.
        /// </summary>
        public event Action<View> DMViewChanged;

        private void OnDMViewChanged(View newView)
        {
            var handler = DMViewChanged;
            if (handler != null) handler(newView);
        }

        /// <summary>
        /// Event triggered when a document other than the
        /// one Dynamo is pointing at becomes active.
        /// </summary>
        public event Action InvalidDMDocumentActivated;

        private void OnInvalidDMDocumentActivated()
        {
            var handler = InvalidDMDocumentActivated;
            if (handler != null) handler();
        }

        protected override void OnWorkspaceRemoveStarted(WorkspaceModel workspace)
        {
            base.OnWorkspaceRemoveStarted(workspace);

           // if (workspace is HomeWorkspaceModel)
               // DisposeLogic.IsClosingHomeworkspace = true;
        }

        protected override void OnWorkspaceRemoved(WorkspaceModel workspace)
        {
            base.OnWorkspaceRemoved(workspace);

            if (workspace is HomeWorkspaceModel)
               // DisposeLogic.IsClosingHomeworkspace = false;

            //Unsubscribe the event
            foreach (var node in workspace.Nodes.ToList())
            {
                //node.PropertyChanged -= node_PropertyChanged;
            }
        }

        protected override void OnWorkspaceAdded(WorkspaceModel workspace)
        {
            base.OnWorkspaceAdded(workspace);

            foreach (var node in workspace.Nodes.ToList())
            {
                //node.PropertyChanged += node_PropertyChanged;
            }
        }



        #endregion

        #region Properties/Fields
  


        #endregion

        #region Constructors

        public new static DynaMayaModel Start()
        {
            return Start(new DMStartConfiguration() { ProcessMode = TaskProcessMode.Asynchronous });
        }

        private static DynaMayaModel Start(DMStartConfiguration dMStartConfiguration)
        {
            throw new NotImplementedException();
        }

     

        private DynaMayaModel(IDMStartConfiguration configuration) :
            base(configuration)
        {
            //MigrationManager.MigrationTargets.Add(typeof(workspacemigra));

            SetupPython();
        }

        private bool isFirstEvaluation = true;

        #endregion


        #region Initialization

        /// <summary>
        /// This call is made during start-up sequence after DMDynamoModel 
        /// constructor returned. Virtual methods on DynamoModel that perform 
        /// initialization steps should only be called from here.
        /// </summary>
        internal void HandlePostInitialization()
        {
          
        }

        private bool setupPython;

        private void SetupPython()
        {
            if (setupPython) return;

           // IronPythonEvaluator.OutputMarshaler.RegisterMarshaler(
           //     (Element element) => element.ToDSType(true));

            // Turn off element binding during iron python script execution
           // IronPythonEvaluator.EvaluationBegin +=
            //    (a, b, c, d, e) => ElementBinder.IsEnabled = false;
           // IronPythonEvaluator.EvaluationEnd += (a, b, c, d, e) => ElementBinder.IsEnabled = true;

            // register UnwrapElement method in ironpython
            /*
            IronPythonEvaluator.EvaluationBegin += (a, b, scope, d, e) =>
            {
                var marshaler = new DataMarshaler();
                marshaler.RegisterMarshaler(
                    (DM.Elements.Element element) => element.InternalElement);
                marshaler.RegisterMarshaler((Category element) => element.InternalCategory);

                Func<object, object> unwrap = marshaler.Marshal;
                scope.SetVariable("UnwrapElement", unwrap);
            };
            */
            setupPython = true;
        }


    

        #endregion




      

        #region Public methods

        public override void OnEvaluationCompleted(object sender, EvaluationCompletedEventArgs e)
        {
          //  Debug.WriteLine(ElementIDLifecycleManager<int>.GetInstance());

     

            base.OnEvaluationCompleted(sender, e);
        }

        protected override void PreShutdownCore(bool shutdownHost)
        {
            if (shutdownHost)
            {
              //  var uiApplication = DocumentManager.Instance.CurrentUIApplication;
                //uiApplication.Idling += ShutdownDMHostOnce;
            }

            base.PreShutdownCore(shutdownHost);
        }

     

        protected override void ShutDownCore(bool shutDownHost)
        {
            base.ShutDownCore(shutDownHost);

        }

        /// <summary>
        /// This event handler is called if 'markNodesAsDirty' in a 
        /// prior call to DMDynamoModel.ResetEngine was set to 'true'.
        /// </summary>
        /// <param name="markNodesAsDirty"></param>
        private void OnResetMarkNodesAsDirty(bool markNodesAsDirty)
        {
            foreach (var workspace in Workspaces.OfType<HomeWorkspaceModel>())
                workspace.ResetEngine(EngineController, markNodesAsDirty);
        }

    
        #endregion

        #region Event handlers

        /// <summary>
        /// Handler DM's DocumentOpened event.
        /// It is called when a document is opened, but NOT when a document is 
        /// created from a template.
        /// </summary>
        private void HandleApplicationDocumentOpened()
        {
            // If the current document is null, for instance if there are
            // no documents open, then set the current document, and 
            // present a message telling us where Dynamo is pointing.
 
        }

 
    
        /// <summary>
        ///     Clears all element collections on nodes and resets the visualization manager and the old value.
        /// </summary>
        private void ResetForNewDocument()
        {
            foreach (var ws in Workspaces.OfType<HomeWorkspaceModel>())
            {
                ws.MarkNodesAsModifiedAndRequestRun(ws.Nodes);
            }

            OnDMDocumentChanged();
        }

     
        #endregion

    }
}