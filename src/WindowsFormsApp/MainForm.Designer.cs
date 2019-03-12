﻿namespace WindowsFormsApp
{
    partial class MainForm
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.updateLogTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // updateLogTextBox
            // 
            this.updateLogTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.updateLogTextBox.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.updateLogTextBox.Location = new System.Drawing.Point(0, 0);
            this.updateLogTextBox.Multiline = true;
            this.updateLogTextBox.Name = "updateLogTextBox";
            this.updateLogTextBox.ReadOnly = true;
            this.updateLogTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.updateLogTextBox.Size = new System.Drawing.Size(784, 561);
            this.updateLogTextBox.TabIndex = 0;
            this.updateLogTextBox.Text = "Version:";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.updateLogTextBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Windows Forms App - MainForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox updateLogTextBox;
    }
}

