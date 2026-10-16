using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ClinicAppointmentManager
{
    public class MainForm : Form
    {
        // ====== Dữ liệu ======
        private static readonly string[] Doctors =
            { "BS. Nguyễn Văn An", "BS. Trần Thị Bình", "BS. Lê Minh Châu", "BS. Phạm Quốc Dũng" };
        private static readonly string[] Statuses = { "Chờ khám", "Đang khám", "Hoàn tất", "Đã hủy" };

        private readonly BindingList<Appointment> _all = new BindingList<Appointment>(); // toàn bộ lịch hẹn
        private readonly BindingSource _bs = new BindingSource();                        // nguồn cho DataGridView (đã lọc)
        private readonly ErrorProvider _err = new ErrorProvider();
        private Appointment _selected;   // null = đang ở chế độ "thêm mới"
        private bool _loading;           // chặn SelectionChanged khi rebind

        // ====== Controls ======
        private TextBox txtId, txtName, txtPhone, txtReason, txtSearch;
        private DateTimePicker dtpTime, dtpFilterDate;
        private ComboBox cboDoctor, cboStatus, cboFilterStatus;
        private CheckBox chkFilterDate;
        private Button btnNew, btnAdd, btnUpdate, btnDelete, btnClearFilter;
        private DataGridView dgv;
        private ToolStripStatusLabel lblCount;

        public MainForm()
        {
            BuildUi();
            SeedData();
            ApplyFilter();
            EnterNewMode();
        }

        // =====================================================================
        //  GIAO DIỆN
        // =====================================================================
        private void BuildUi()
        {
            Text = "Clinic Appointment Manager";
            Font = new Font("Segoe UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 700);
            MinimumSize = new Size(900, 600);

            // Bố cục gốc: nhập liệu / bộ lọc / lưới (co giãn)
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            root.Controls.Add(BuildInputGroup(), 0, 0);
            root.Controls.Add(BuildFilterGroup(), 0, 1);
            root.Controls.Add(BuildGrid(), 0, 2);

            // Menu + StatusStrip
            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("&Tệp");
            var miExport = new ToolStripMenuItem("&Xuất danh sách ra CSV...", null, (s, e) => ExportCsv())
            { ShortcutKeys = Keys.Control | Keys.E };
            var miExit = new ToolStripMenuItem("T&hoát", null, (s, e) => Close());
            mFile.DropDownItems.AddRange(new ToolStripItem[] { miExport, new ToolStripSeparator(), miExit });
            menu.Items.Add(mFile);

            var status = new StatusStrip();
            lblCount = new ToolStripStatusLabel();
            status.Items.Add(lblCount);

            // Thứ tự add: control Fill trước, thanh menu/status sau
            Controls.Add(root);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;

            _err.BlinkStyle = ErrorBlinkStyle.NeverBlink;
        }

        private Control BuildInputGroup()
        {
            var grp = new GroupBox { Text = "Thông tin lịch hẹn", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 5 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 5; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtId = new TextBox();
            txtName = new TextBox();
            txtPhone = new TextBox();
            txtReason = new TextBox { Multiline = true, Height = 50 };
            dtpTime = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm" };
            cboDoctor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboDoctor.Items.AddRange(Doctors);
            cboStatus.Items.AddRange(Statuses);

            AddRow(t, 0, "Mã lịch hẹn:", txtId, "Họ tên bệnh nhân:", txtName);
            AddRow(t, 1, "Số điện thoại:", txtPhone, "Ngày giờ hẹn:", dtpTime);
            AddRow(t, 2, "Bác sĩ:", cboDoctor, "Trạng thái:", cboStatus);

            t.Controls.Add(MakeLabel("Lý do khám:"), 0, 3);
            t.Controls.Add(txtReason, 1, 3);
            t.SetColumnSpan(txtReason, 3);
            txtReason.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtReason.Margin = new Padding(3, 4, 3, 4);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            btnNew = new Button { Text = "Mới", AutoSize = true };
            btnAdd = new Button { Text = "Thêm", AutoSize = true };
            btnUpdate = new Button { Text = "Cập nhật", AutoSize = true };
            btnDelete = new Button { Text = "Xóa", AutoSize = true };
            buttons.Controls.AddRange(new Control[] { btnNew, btnAdd, btnUpdate, btnDelete });
            t.Controls.Add(buttons, 0, 4);
            t.SetColumnSpan(buttons, 4);

            btnNew.Click += (s, e) => EnterNewMode();
            btnAdd.Click += (s, e) => AddAppointment();
            btnUpdate.Click += (s, e) => UpdateAppointment();
            btnDelete.Click += (s, e) => DeleteAppointment();

            // Báo lỗi ngay khi rời khỏi từng trường
            txtId.Leave += (s, e) => { if (!txtId.ReadOnly) ValidateId(true); };
            txtName.Leave += (s, e) => ValidateName();
            txtPhone.Leave += (s, e) => ValidatePhone();
            txtReason.Leave += (s, e) => ValidateReason();
            dtpTime.Leave += (s, e) => ValidateDate(_selected == null);

            grp.Controls.Add(t);
            return grp;
        }

        private Control BuildFilterGroup()
        {
            var grp = new GroupBox { Text = "Tìm kiếm & lọc", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 7, RowCount = 1 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtSearch = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            chkFilterDate = new CheckBox { Text = "Ngày hẹn:", AutoSize = true, Anchor = AnchorStyles.Left };
            dtpFilterDate = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 120, Enabled = false };
            cboFilterStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
            cboFilterStatus.Items.Add("Tất cả");
            cboFilterStatus.Items.AddRange(Statuses);
            cboFilterStatus.SelectedIndex = 0;
            btnClearFilter = new Button { Text = "Bỏ lọc", AutoSize = true };

            t.Controls.Add(MakeLabel("Tìm (tên / SĐT):"), 0, 0);
            t.Controls.Add(txtSearch, 1, 0);
            t.Controls.Add(chkFilterDate, 2, 0);
            t.Controls.Add(dtpFilterDate, 3, 0);
            t.Controls.Add(MakeLabel("Trạng thái:"), 4, 0);
            t.Controls.Add(cboFilterStatus, 5, 0);
            t.Controls.Add(btnClearFilter, 6, 0);

            txtSearch.TextChanged += (s, e) => ApplyFilter();           // tìm kiếm tức thời
            chkFilterDate.CheckedChanged += (s, e) => { dtpFilterDate.Enabled = chkFilterDate.Checked; ApplyFilter(); };
            dtpFilterDate.ValueChanged += (s, e) => ApplyFilter();
            cboFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilter();
            btnClearFilter.Click += (s, e) =>
            {
                txtSearch.Text = "";
                chkFilterDate.Checked = false;
                cboFilterStatus.SelectedIndex = 0;
                ApplyFilter();
            };

            grp.Controls.Add(t);
            return grp;
        }

        private Control BuildGrid()
        {
            dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            dgv.Columns.Add(Col("Id", "Mã LH", 70));
            dgv.Columns.Add(Col("PatientName", "Bệnh nhân", 150));
            dgv.Columns.Add(Col("Phone", "Số điện thoại", 100));
            var colTime = Col("AppointmentTime", "Ngày giờ hẹn", 120);
            colTime.DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
            dgv.Columns.Add(colTime);
            dgv.Columns.Add(Col("Doctor", "Bác sĩ", 140));
            dgv.Columns.Add(Col("Reason", "Lý do khám", 220));
            dgv.Columns.Add(Col("Status", "Trạng thái", 90));

            dgv.DataSource = _bs;
            dgv.SelectionChanged += Dgv_SelectionChanged;
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || dgv.Columns[e.ColumnIndex].DataPropertyName != "Status") return;
                switch (e.Value as string)
                {
                    case "Đang khám": e.CellStyle.ForeColor = Color.DarkOrange; break;
                    case "Hoàn tất": e.CellStyle.ForeColor = Color.SeaGreen; break;
                    case "Đã hủy": e.CellStyle.ForeColor = Color.Firebrick; break;
                }
            };
            return dgv;
        }

        private static DataGridViewTextBoxColumn Col(string prop, string header, float weight) =>
            new DataGridViewTextBoxColumn { DataPropertyName = prop, HeaderText = header, Name = "col" + prop, FillWeight = weight };

        private static Label MakeLabel(string text) =>
            new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 8, 8) };

        private static void AddRow(TableLayoutPanel t, int row, string l1, Control c1, string l2, Control c2)
        {
            t.Controls.Add(MakeLabel(l1), 0, row);
            t.Controls.Add(c1, 1, row);
            t.Controls.Add(MakeLabel(l2), 2, row);
            t.Controls.Add(c2, 3, row);
            c1.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            c2.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }

        // =====================================================================
        //  DỮ LIỆU MẪU
        // =====================================================================
        private void SeedData()
        {
            DateTime b = RoundToMinute(DateTime.Now).AddHours(1);
            _all.Add(new Appointment { Id = "LH001", PatientName = "Nguyễn Thị Hoa", Phone = "0912345678", AppointmentTime = b, Doctor = Doctors[0], Reason = "Đau đầu kéo dài", Status = "Chờ khám" });
            _all.Add(new Appointment { Id = "LH002", PatientName = "Trần Văn Nam", Phone = "0987654321", AppointmentTime = b.AddHours(1), Doctor = Doctors[1], Reason = "Khám tổng quát", Status = "Chờ khám" });
            _all.Add(new Appointment { Id = "LH003", PatientName = "Lê Thị Mai", Phone = "0905111222", AppointmentTime = b.AddDays(1), Doctor = Doctors[2], Reason = "Tái khám tiểu đường", Status = "Chờ khám" });
            _all.Add(new Appointment { Id = "LH004", PatientName = "Phạm Đức Anh", Phone = "0333444555", AppointmentTime = b.AddDays(1).AddHours(2), Doctor = Doctors[3], Reason = "Đau lưng", Status = "Đang khám" });
        }

        // =====================================================================
        //  LỌC / TÌM KIẾM
        // =====================================================================
        private void ApplyFilter()
        {
            if (dgv == null) return;
            string q = Norm(txtSearch.Text);
            string qPhone = txtSearch.Text.Replace(" ", "").Replace(".", "").Replace("-", "");
            string st = cboFilterStatus.SelectedIndex > 0 ? cboFilterStatus.SelectedItem.ToString() : null;

            var result = _all.Where(a =>
                (q.Length == 0 || Norm(a.PatientName).Contains(q) ||
                    (qPhone.Length > 0 && a.Phone.Contains(qPhone))) &&
                (!chkFilterDate.Checked || a.AppointmentTime.Date == dtpFilterDate.Value.Date) &&
                (st == null || a.Status == st)).ToList();

            var keep = _selected;
            _loading = true;
            _bs.DataSource = new BindingList<Appointment>(result);
            int idx = keep == null ? -1 : result.IndexOf(keep);
            if (idx >= 0)
            {
                dgv.Rows[idx].Selected = true;
                dgv.CurrentCell = dgv.Rows[idx].Cells[0];
            }
            else
            {
                dgv.CurrentCell = null;
                dgv.ClearSelection();
            }
            _loading = false;

            if (keep != null && idx < 0) EnterNewMode();   // dòng đang sửa đã bị lọc mất/xóa
            UpdateStatusBar();
        }

        private void UpdateStatusBar() =>
            lblCount.Text = $"Số lịch hẹn sau khi lọc: {_bs.Count} / {_all.Count}";

        private static string Norm(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant().Trim();
        }

        // =====================================================================
        //  CHỌN DÒNG <-> FORM
        // =====================================================================
        private void Dgv_SelectionChanged(object sender, EventArgs e)
        {
            if (_loading || dgv.SelectedRows.Count == 0) return;
            var a = dgv.SelectedRows[0].DataBoundItem as Appointment;
            if (a == null) return;

            _selected = a;
            txtId.Text = a.Id;
            txtId.ReadOnly = true;
            txtName.Text = a.PatientName;
            txtPhone.Text = a.Phone;
            dtpTime.Value = a.AppointmentTime;
            cboDoctor.SelectedItem = a.Doctor;
            cboStatus.SelectedItem = a.Status;
            txtReason.Text = a.Reason;
            _err.Clear();
            SetModeButtons();
        }

        private void EnterNewMode()
        {
            _selected = null;
            _loading = true;
            dgv.CurrentCell = null;
            dgv.ClearSelection();
            _loading = false;

            txtId.ReadOnly = false;
            txtId.Text = NextId();
            txtName.Text = "";
            txtPhone.Text = "";
            txtReason.Text = "";
            dtpTime.Value = RoundToMinute(DateTime.Now).AddHours(1);
            cboDoctor.SelectedIndex = 0;
            cboStatus.SelectedIndex = 0;
            _err.Clear();
            SetModeButtons();
            txtName.Focus();
        }

        private void SetModeButtons()
        {
            btnAdd.Enabled = _selected == null;
            btnUpdate.Enabled = _selected != null;
            btnDelete.Enabled = _selected != null;
        }

        private string NextId()
        {
            int n = _all.Count + 1;
            while (_all.Any(a => a.Id.Equals("LH" + n.ToString("D3"), StringComparison.OrdinalIgnoreCase))) n++;
            return "LH" + n.ToString("D3");
        }

        // =====================================================================
        //  THÊM / SỬA / XÓA
        // =====================================================================
        private void AddAppointment()
        {
            if (!ValidateAll(true)) return;
            if (!Confirm("Thêm lịch hẹn này?")) return;

            var a = new Appointment();
            ReadForm(a);
            _all.Add(a);
            EnterNewMode();
            ApplyFilter();
        }

        private void UpdateAppointment()
        {
            if (_selected == null) return;
            if (!ValidateAll(false)) return;
            if (!Confirm($"Cập nhật lịch hẹn {_selected.Id}?")) return;

            ReadForm(_selected);
            ApplyFilter();   // làm mới lưới + thanh trạng thái (nếu trạng thái không còn khớp bộ lọc, dòng sẽ ẩn)
        }

        private void DeleteAppointment()
        {
            if (_selected == null) return;
            if (!Confirm($"Xóa lịch hẹn {_selected.Id} của {_selected.PatientName}?")) return;

            _all.Remove(_selected);
            _selected = null;
            ApplyFilter();
            EnterNewMode();
        }

        private void ReadForm(Appointment a)
        {
            a.Id = txtId.Text.Trim();
            a.PatientName = txtName.Text.Trim();
            a.Phone = CleanPhone(txtPhone.Text);
            a.AppointmentTime = RoundToMinute(dtpTime.Value);
            a.Doctor = cboDoctor.SelectedItem.ToString();
            a.Status = cboStatus.SelectedItem.ToString();
            a.Reason = txtReason.Text.Trim();
        }

        private bool Confirm(string msg) =>
            MessageBox.Show(msg, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        // =====================================================================
        //  KIỂM TRA DỮ LIỆU (ErrorProvider)
        // =====================================================================
        private static readonly Regex PhoneRegex = new Regex(@"^(\+84|0)\d{9,10}$");

        private static string CleanPhone(string s) =>
            (s ?? "").Replace(" ", "").Replace(".", "").Replace("-", "");

        private static DateTime RoundToMinute(DateTime d) =>
            new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0);

        private bool ValidateAll(bool isNew)
        {
            // Dùng & (không rút gọn) để hiện tất cả lỗi cùng lúc
            return ValidateId(isNew) & ValidateName() & ValidatePhone() & ValidateDate(isNew) & ValidateReason();
        }

        private bool ValidateId(bool isNew)
        {
            string id = txtId.Text.Trim();
            if (id.Length == 0) return SetError(txtId, "Mã lịch hẹn không được để trống");
            if (isNew && _all.Any(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                return SetError(txtId, "Mã lịch hẹn đã tồn tại");
            return SetError(txtId, null);
        }

        private bool ValidateName() =>
            SetError(txtName, txtName.Text.Trim().Length == 0 ? "Vui lòng nhập họ tên bệnh nhân" : null);

        private bool ValidatePhone()
        {
            string p = CleanPhone(txtPhone.Text);
            if (p.Length == 0) return SetError(txtPhone, "Vui lòng nhập số điện thoại");
            if (!PhoneRegex.IsMatch(p)) return SetError(txtPhone, "Số điện thoại không hợp lệ (VD: 0912345678 hoặc +84912345678)");
            return SetError(txtPhone, null);
        }

        private bool ValidateReason() =>
            SetError(txtReason, txtReason.Text.Trim().Length == 0 ? "Vui lòng nhập lý do khám" : null);

        private bool ValidateDate(bool isNew)
        {
            DateTime v = RoundToMinute(dtpTime.Value);
            // Khi sửa mà không đổi giờ hẹn thì cho phép (VD: chuyển trạng thái lịch đã tới giờ)
            bool unchanged = !isNew && _selected != null && v == RoundToMinute(_selected.AppointmentTime);
            if (!unchanged && v < RoundToMinute(DateTime.Now))
                return SetError(dtpTime, "Ngày giờ hẹn không được nằm trong quá khứ");
            return SetError(dtpTime, null);
        }

        private bool SetError(Control c, string message)
        {
            _err.SetError(c, message ?? "");
            return message == null;
        }

        // =====================================================================
        //  XUẤT CSV
        // =====================================================================
        private void ExportCsv()
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"lich_hen_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Xuất danh sách lịch hẹn đang hiển thị"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Mã lịch hẹn,Họ tên,Số điện thoại,Ngày giờ hẹn,Bác sĩ,Lý do khám,Trạng thái");
                foreach (Appointment a in _bs.List)
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        Csv(a.Id), Csv(a.PatientName), Csv("'" + a.Phone), // dấu ' giữ số 0 đầu khi mở bằng Excel
                        Csv(a.AppointmentTime.ToString("dd/MM/yyyy HH:mm")),
                        Csv(a.Doctor), Csv(a.Reason), Csv(a.Status)
                    }));
                }
                try
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show($"Đã xuất {_bs.Count} lịch hẹn.", "Xuất CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Không thể ghi file: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string Csv(string s)
        {
            s = s ?? "";
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
