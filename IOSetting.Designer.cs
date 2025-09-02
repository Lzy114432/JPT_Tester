
namespace SRTLogic
{
    partial class IOSetting
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.ok_button = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.outputGridView = new System.Windows.Forms.DataGridView();
            this.inputGridView = new System.Windows.Forms.DataGridView();
            this.dataGridViewTextBoxColumn5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn8 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn4 = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.groupBox1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.outputGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.inputGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // ok_button
            // 
            this.ok_button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ok_button.Location = new System.Drawing.Point(444, 627);
            this.ok_button.Name = "ok_button";
            this.ok_button.Size = new System.Drawing.Size(143, 43);
            this.ok_button.TabIndex = 4;
            this.ok_button.Text = "确定";
            this.ok_button.UseVisualStyleBackColor = true;
            this.ok_button.Click += new System.EventHandler(this.ok_button_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tabControl1);
            this.groupBox1.Controls.Add(this.ok_button);
            this.groupBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox1.Location = new System.Drawing.Point(0, 0);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(593, 676);
            this.groupBox1.TabIndex = 5;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "映射设置";
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(13, 29);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(568, 592);
            this.tabControl1.TabIndex = 5;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage1.Controls.Add(this.inputGridView);
            this.tabPage1.Location = new System.Drawing.Point(4, 30);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(560, 558);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "输入";
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.outputGridView);
            this.tabPage2.Location = new System.Drawing.Point(4, 30);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(560, 558);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "输出";
            // 
            // outputGridView
            // 
            this.outputGridView.AllowUserToAddRows = false;
            this.outputGridView.AllowUserToDeleteRows = false;
            this.outputGridView.AllowUserToResizeColumns = false;
            this.outputGridView.AllowUserToResizeRows = false;
            this.outputGridView.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            this.outputGridView.BackgroundColor = System.Drawing.Color.White;
            this.outputGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.outputGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dataGridViewTextBoxColumn5,
            this.dataGridViewTextBoxColumn6,
            this.dataGridViewTextBoxColumn7,
            this.dataGridViewTextBoxColumn8});
            this.outputGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.outputGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.outputGridView.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.outputGridView.Location = new System.Drawing.Point(3, 3);
            this.outputGridView.MultiSelect = false;
            this.outputGridView.Name = "outputGridView";
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            this.outputGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;
            this.outputGridView.RowHeadersVisible = false;
            this.outputGridView.RowTemplate.Height = 23;
            this.outputGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.outputGridView.Size = new System.Drawing.Size(554, 552);
            this.outputGridView.TabIndex = 5;
            // 
            // inputGridView
            // 
            this.inputGridView.AllowUserToAddRows = false;
            this.inputGridView.AllowUserToDeleteRows = false;
            this.inputGridView.AllowUserToResizeColumns = false;
            this.inputGridView.AllowUserToResizeRows = false;
            this.inputGridView.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;
            this.inputGridView.BackgroundColor = System.Drawing.Color.White;
            this.inputGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.inputGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dataGridViewTextBoxColumn1,
            this.dataGridViewTextBoxColumn2,
            this.dataGridViewTextBoxColumn3,
            this.dataGridViewTextBoxColumn4});
            this.inputGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.inputGridView.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.inputGridView.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.inputGridView.Location = new System.Drawing.Point(3, 3);
            this.inputGridView.MultiSelect = false;
            this.inputGridView.Name = "inputGridView";
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            this.inputGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.inputGridView.RowHeadersVisible = false;
            this.inputGridView.RowTemplate.Height = 23;
            this.inputGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.inputGridView.Size = new System.Drawing.Size(554, 552);
            this.inputGridView.TabIndex = 6;
            // 
            // dataGridViewTextBoxColumn5
            // 
            this.dataGridViewTextBoxColumn5.Frozen = true;
            this.dataGridViewTextBoxColumn5.HeaderText = "序号";
            this.dataGridViewTextBoxColumn5.Name = "dataGridViewTextBoxColumn5";
            this.dataGridViewTextBoxColumn5.ReadOnly = true;
            this.dataGridViewTextBoxColumn5.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn5.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumn6
            // 
            this.dataGridViewTextBoxColumn6.Frozen = true;
            this.dataGridViewTextBoxColumn6.HeaderText = "名称";
            this.dataGridViewTextBoxColumn6.Name = "dataGridViewTextBoxColumn6";
            this.dataGridViewTextBoxColumn6.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn6.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.dataGridViewTextBoxColumn6.Width = 250;
            // 
            // dataGridViewTextBoxColumn7
            // 
            this.dataGridViewTextBoxColumn7.Frozen = true;
            this.dataGridViewTextBoxColumn7.HeaderText = "映射";
            this.dataGridViewTextBoxColumn7.Name = "dataGridViewTextBoxColumn7";
            this.dataGridViewTextBoxColumn7.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn7.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumn8
            // 
            this.dataGridViewTextBoxColumn8.Frozen = true;
            this.dataGridViewTextBoxColumn8.HeaderText = "状态";
            this.dataGridViewTextBoxColumn8.Items.AddRange(new object[] {
            "常开",
            "常闭"});
            this.dataGridViewTextBoxColumn8.Name = "dataGridViewTextBoxColumn8";
            this.dataGridViewTextBoxColumn8.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.Frozen = true;
            this.dataGridViewTextBoxColumn1.HeaderText = "序号";
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            this.dataGridViewTextBoxColumn1.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.Frozen = true;
            this.dataGridViewTextBoxColumn2.HeaderText = "名称";
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            this.dataGridViewTextBoxColumn2.Width = 250;
            // 
            // dataGridViewTextBoxColumn3
            // 
            this.dataGridViewTextBoxColumn3.Frozen = true;
            this.dataGridViewTextBoxColumn3.HeaderText = "映射";
            this.dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            this.dataGridViewTextBoxColumn3.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewTextBoxColumn3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumn4
            // 
            this.dataGridViewTextBoxColumn4.Frozen = true;
            this.dataGridViewTextBoxColumn4.HeaderText = "状态";
            this.dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
            this.dataGridViewTextBoxColumn4.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // IOSetting
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(10F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(593, 676);
            this.Controls.Add(this.groupBox1);
            this.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "IOSetting";
            this.Text = "IO配置";
            this.groupBox1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.outputGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.inputGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button ok_button;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridView inputGridView;
        private System.Windows.Forms.DataGridView outputGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn5;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn6;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn7;
        private System.Windows.Forms.DataGridViewComboBoxColumn dataGridViewTextBoxColumn8;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private System.Windows.Forms.DataGridViewComboBoxColumn dataGridViewTextBoxColumn4;
    }
}