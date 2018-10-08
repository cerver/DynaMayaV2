using System;
using System.IO;
using System.Reflection;
using Dynamo.Models;

namespace Dynamo.Applications.Models
{
    public class MayaDynamoModel : DynamoModel
    {
        /// <summary>
        ///     Flag for syncing up document switches between Application.DocumentClosing and
        ///     Application.DocumentClosed events.
        /// </summary>
        private bool updateCurrentUIDoc;

        #region Events

        public event EventHandler MayaDocumentChanged;
        /*
        public virtual void OnMayaDocumentChanged()
        {
            if (RevitDocumentChanged != null)
                RevitDocumentChanged(this, EventArgs.Empty);
        }
      */

        #endregion

        #region Initialization

        /// <summary>
        ///     This call is made during start-up sequence after MayaDynamoModel
        ///     constructor returned. Virtual methods on DynamoModel that perform
        ///     initialization steps should only be called from here.
        /// </summary>
        internal void HandlePostInitialization()
        {
        }

        #endregion

        #region Properties/Fields

        #endregion

        #region Constructors

        public new static MayaDynamoModel Start()
        {
            return Start(new DefaultStartConfiguration());
        }


        public new static MayaDynamoModel Start(IStartConfiguration configuration)
        {
            var dsc = new DefaultStartConfiguration();


            // where necessary, assign defaults
            if (string.IsNullOrEmpty(dsc.Context))
                dsc.Context = Core.Context.REVIT_2015;
            if (string.IsNullOrEmpty(dsc.DynamoCorePath))
            {
                var asmLocation = Assembly.GetExecutingAssembly().Location;
                dsc.DynamoCorePath = Path.GetDirectoryName(asmLocation);
            }

            if (dsc.Preferences == null)
                dsc.Preferences = new PreferenceSettings();


            return new MayaDynamoModel(configuration);
        }

        private MayaDynamoModel(IStartConfiguration configuration) :
            base(configuration)
        {
        }

        #endregion
    }
}