namespace OldNewGateway
{
    partial class OldNewGateway
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.myEventLog = new System.Diagnostics.EventLog();
            this.worker = new System.ComponentModel.BackgroundWorker();
            ((System.ComponentModel.ISupportInitialize)(this.myEventLog)).BeginInit();
            // 
            // worker
            // 
            this.worker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.worker_DoWork);
            // 
            // OldNewGateway
            // 
            this.ServiceName = "OldNewGateway";
            ((System.ComponentModel.ISupportInitialize)(this.myEventLog)).EndInit();

        }

        #endregion

        private System.Diagnostics.EventLog myEventLog;
        private System.ComponentModel.BackgroundWorker worker;
    }
}
