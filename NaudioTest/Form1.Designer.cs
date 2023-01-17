
namespace NaudioTest
{
    partial class Form1
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cb_Mic = new System.Windows.Forms.ComboBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.cb_Speaker = new System.Windows.Forms.ComboBox();
            this.btn_RecordStart = new System.Windows.Forms.Button();
            this.btn_Stop = new System.Windows.Forms.Button();
            this.btn_FileEncoding = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cb_Mic);
            this.groupBox1.Location = new System.Drawing.Point(43, 37);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(368, 83);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "마이크";
            // 
            // cb_Mic
            // 
            this.cb_Mic.Font = new System.Drawing.Font("굴림", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.cb_Mic.FormattingEnabled = true;
            this.cb_Mic.Location = new System.Drawing.Point(16, 34);
            this.cb_Mic.Name = "cb_Mic";
            this.cb_Mic.Size = new System.Drawing.Size(328, 27);
            this.cb_Mic.TabIndex = 0;
            this.cb_Mic.SelectedIndexChanged += new System.EventHandler(this.cb_Mic_SelectedIndexChanged);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.cb_Speaker);
            this.groupBox2.Location = new System.Drawing.Point(43, 126);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(368, 83);
            this.groupBox2.TabIndex = 2;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "스피커";
            // 
            // cb_Speaker
            // 
            this.cb_Speaker.Font = new System.Drawing.Font("굴림", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.cb_Speaker.FormattingEnabled = true;
            this.cb_Speaker.Location = new System.Drawing.Point(16, 34);
            this.cb_Speaker.Name = "cb_Speaker";
            this.cb_Speaker.Size = new System.Drawing.Size(328, 27);
            this.cb_Speaker.TabIndex = 0;
            this.cb_Speaker.SelectedIndexChanged += new System.EventHandler(this.cb_Speaker_SelectedIndexChanged);
            // 
            // btn_RecordStart
            // 
            this.btn_RecordStart.Location = new System.Drawing.Point(43, 238);
            this.btn_RecordStart.Name = "btn_RecordStart";
            this.btn_RecordStart.Size = new System.Drawing.Size(100, 52);
            this.btn_RecordStart.TabIndex = 3;
            this.btn_RecordStart.Text = "녹음 시작";
            this.btn_RecordStart.UseVisualStyleBackColor = true;
            this.btn_RecordStart.Click += new System.EventHandler(this.btn_RecordStart_Click);
            // 
            // btn_Stop
            // 
            this.btn_Stop.Location = new System.Drawing.Point(149, 238);
            this.btn_Stop.Name = "btn_Stop";
            this.btn_Stop.Size = new System.Drawing.Size(111, 52);
            this.btn_Stop.TabIndex = 4;
            this.btn_Stop.Text = "녹음 중지";
            this.btn_Stop.UseVisualStyleBackColor = true;
            this.btn_Stop.Click += new System.EventHandler(this.btn_Stop_Click);
            // 
            // btn_FileEncoding
            // 
            this.btn_FileEncoding.Location = new System.Drawing.Point(266, 238);
            this.btn_FileEncoding.Name = "btn_FileEncoding";
            this.btn_FileEncoding.Size = new System.Drawing.Size(111, 52);
            this.btn_FileEncoding.TabIndex = 5;
            this.btn_FileEncoding.Text = "파일 인코딩";
            this.btn_FileEncoding.UseVisualStyleBackColor = true;
            this.btn_FileEncoding.Click += new System.EventHandler(this.btn_FileEncoding_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(579, 302);
            this.Controls.Add(this.btn_FileEncoding);
            this.Controls.Add(this.btn_Stop);
            this.Controls.Add(this.btn_RecordStart);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ComboBox cb_Mic;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox cb_Speaker;
        private System.Windows.Forms.Button btn_RecordStart;
        private System.Windows.Forms.Button btn_Stop;
        private System.Windows.Forms.Button btn_FileEncoding;
    }
}

