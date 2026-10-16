using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LabEquipmentCheckout
{
    public class MainForm : Form
    {
        private static readonly string[] EquipmentList =
        {
            "Bộ kit Arduino Uno", "Bộ Raspberry Pi 4", "Đồng hồ vạn năng", "Máy hiện sóng (Oscilloscope)",
            "Máy hàn thiếc", "Kính hiển vi", "Laptop thực hành"
        };

        private readonly BindingList<LoanSlip> _all = new BindingList<LoanSlip>();   // toàn bộ phiếu
        private readonly BindingSource _bs = new BindingSource();                     // nguồn của lưới (đã lọc)
        private readonly ErrorProvider _err = new ErrorProvider();
        private LoanSlip _selected;   // null = chế độ thêm mới
        private bool _loading;

        private TextBox txtId, txtStudentId, txtStudentName, txtStatus, txtSearch;
        private ComboBox cboEquipment, cboFilterStatus;
        private NumericUpDown nudQty;
        private DateTimePicker dtpBorrow, dtpDue, dtpReturn;
        private CheckBox chkReturned;
        private Button btnNew, btnAdd, btnUpdate, btnDelete, btnReturn, btnClearFilter;
        private DataGridView dgv;
        private ToolStripStatusLabel lblStatus;

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
            Text = "Lab Equipment Checkout";
            Font = new Font("Segoe UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1150, 720);
            MinimumSize = new Size(950, 620);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildInputGroup(), 0, 0);
            root.Controls.Add(BuildFilterGroup(), 0, 1);
            root.Controls.Add(BuildGrid(), 0, 2);

            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("&Tệp");
            mFile.DropDownItems.Add(new ToolStripMenuItem("&Xuất phiếu đang lọc ra CSV...", null, (s, e) => ExportCsv())
            { ShortcutKeys = Keys.Control | Keys.E });
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add(new ToolStripMenuItem("T&hoát", null, (s, e) => Close()));
            menu.Items.Add(mFile);

            var status = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            status.Items.Add(lblStatus);

            Controls.Add(root);
            Controls.Add(status);
            Controls.Add(menu);
            MainMenuStrip = menu;

            _err.BlinkStyle = ErrorBlinkStyle.NeverBlink;
        }

        private Control BuildInputGroup()
        {
            var grp = new GroupBox { Text = "Phiếu mượn", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 6 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtId = new TextBox();
            txtStudentId = new TextBox();
            txtStudentName = new TextBox();
            txtStatus = new TextBox { ReadOnly = true, TabStop = false };
            cboEquipment = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboEquipment.Items.AddRange(EquipmentList);
            nudQty = new NumericUpDown { Minimum = 0, Maximum = 1000, Value = 1 };
            dtpBorrow = new DateTimePicker { Format = DateTimePickerFormat.Short };
            dtpDue = new DateTimePicker { Format = DateTimePickerFormat.Short };
            dtpReturn = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 140, Enabled = false };
            chkReturned = new CheckBox { Text = "Đã trả", AutoSize = true, Anchor = AnchorStyles.Left };

            AddRow(t, 0, "Mã phiếu:", txtId, "Thiết bị:", cboEquipment);
            AddRow(t, 1, "Mã sinh viên:", txtStudentId, "Họ tên sinh viên:", txtStudentName);
            AddRow(t, 2, "Số lượng:", nudQty, "Tình trạng:", txtStatus);
            AddRow(t, 3, "Ngày mượn:", dtpBorrow, "Hạn trả:", dtpDue);

            var returnPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Left, WrapContents = false };
            returnPanel.Controls.Add(chkReturned);
            returnPanel.Controls.Add(dtpReturn);
            t.Controls.Add(MakeLabel("Ngày trả thực tế:"), 0, 4);
            t.Controls.Add(returnPanel, 1, 4);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            btnNew = new Button { Text = "Mới", AutoSize = true };
            btnAdd = new Button { Text = "Thêm", AutoSize = true };
            btnUpdate = new Button { Text = "Cập nhật", AutoSize = true };
            btnDelete = new Button { Text = "Xóa", AutoSize = true };
            btnReturn = new Button { Text = "Đánh dấu đã trả", AutoSize = true };
            buttons.Controls.AddRange(new Control[] { btnNew, btnAdd, btnUpdate, btnDelete, btnReturn });
            t.Controls.Add(buttons, 0, 5);
            t.SetColumnSpan(buttons, 4);

            btnNew.Click += (s, e) => EnterNewMode();
            btnAdd.Click += (s, e) => AddSlip();
            btnUpdate.Click += (s, e) => UpdateSlip();
            btnDelete.Click += (s, e) => DeleteSlip();
            btnReturn.Click += (s, e) => MarkReturned();

            // Kiểm tra tức thời + cập nhật tình trạng xem trước
            txtId.Leave += (s, e) => { if (!txtId.ReadOnly) ValidateId(true); };
            txtStudentId.Leave += (s, e) => ValidateStudentId();
            txtStudentName.Leave += (s, e) => ValidateStudentName();
            cboEquipment.SelectedIndexChanged += (s, e) => ValidateEquipment();
            nudQty.ValueChanged += (s, e) => ValidateQty();
            dtpBorrow.ValueChanged += (s, e) => { ValidateDates(); ValidateReturn(); UpdateStatusPreview(); };
            dtpDue.ValueChanged += (s, e) => { ValidateDates(); UpdateStatusPreview(); };
            dtpReturn.ValueChanged += (s, e) => { ValidateReturn(); UpdateStatusPreview(); };
            chkReturned.CheckedChanged += (s, e) =>
            {
                dtpReturn.Enabled = chkReturned.Checked;
                ValidateReturn();
                UpdateStatusPreview();
            };

            grp.Controls.Add(t);
            return grp;
        }

        private Control BuildFilterGroup()
        {
            var grp = new GroupBox { Text = "Tìm kiếm & lọc", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, RowCount = 1 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            txtSearch = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            cboFilterStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
            cboFilterStatus.Items.AddRange(new object[] { "Tất cả", LoanSlip.Borrowing, LoanSlip.Overdue, LoanSlip.Returned });
            cboFilterStatus.SelectedIndex = 0;
            btnClearFilter = new Button { Text = "Bỏ lọc", AutoSize = true };

            t.Controls.Add(MakeLabel("Tìm (mã SV / tên thiết bị):"), 0, 0);
            t.Controls.Add(txtSearch, 1, 0);
            t.Controls.Add(MakeLabel("Tình trạng:"), 2, 0);
            t.Controls.Add(cboFilterStatus, 3, 0);
            t.Controls.Add(btnClearFilter, 4, 0);

            txtSearch.TextChanged += (s, e) => ApplyFilter();
            cboFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilter();
            btnClearFilter.Click += (s, e) => { txtSearch.Text = ""; cboFilterStatus.SelectedIndex = 0; ApplyFilter(); };

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
            dgv.Columns.Add(Col("Id", "Mã phiếu", 70));
            dgv.Columns.Add(Col("StudentId", "Mã SV", 80));
            dgv.Columns.Add(Col("StudentName", "Họ tên SV", 140));
            dgv.Columns.Add(Col("Equipment", "Thiết bị", 170));
            var colQty = Col("Quantity", "SL", 40);
            colQty.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.Columns.Add(colQty);
            var colB = Col("BorrowDate", "Ngày mượn", 90);
            colB.DefaultCellStyle.Format = "dd/MM/yyyy";
            dgv.Columns.Add(colB);
            var colD = Col("DueDate", "Hạn trả", 90);
            colD.DefaultCellStyle.Format = "dd/MM/yyyy";
            dgv.Columns.Add(colD);
            dgv.Columns.Add(Col("ReturnDateText", "Ngày trả thực tế", 100));
            dgv.Columns.Add(Col("Status", "Tình trạng", 90));

            dgv.DataSource = _bs;
            dgv.SelectionChanged += Dgv_SelectionChanged;
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                var slip = dgv.Rows[e.RowIndex].DataBoundItem as LoanSlip;
                if (slip == null) return;
                if (slip.Status == LoanSlip.Overdue)
                {
                    e.CellStyle.BackColor = Color.MistyRose;
                    e.CellStyle.ForeColor = Color.Firebrick;
                }
                else if (slip.Status == LoanSlip.Returned)
                {
                    e.CellStyle.ForeColor = Color.SeaGreen;
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
            DateTime d = DateTime.Today;
            _all.Add(new LoanSlip { Id = "PM001", StudentId = "SV001", StudentName = "Nguyễn Văn An", Equipment = EquipmentList[0], Quantity = 2, BorrowDate = d.AddDays(-10), DueDate = d.AddDays(-3) });   // quá hạn
            _all.Add(new LoanSlip { Id = "PM002", StudentId = "SV002", StudentName = "Trần Thị Bình", Equipment = EquipmentList[2], Quantity = 1, BorrowDate = d.AddDays(-2), DueDate = d.AddDays(5) });     // đang mượn
            _all.Add(new LoanSlip { Id = "PM003", StudentId = "SV003", StudentName = "Lê Minh Châu", Equipment = EquipmentList[5], Quantity = 1, BorrowDate = d.AddDays(-8), DueDate = d.AddDays(-1), ReturnDate = d.AddDays(-2) }); // đã trả
            _all.Add(new LoanSlip { Id = "PM004", StudentId = "SV004", StudentName = "Phạm Quốc Dũng", Equipment = EquipmentList[6], Quantity = 1, BorrowDate = d, DueDate = d.AddDays(7) });               // đang mượn
        }

        // =====================================================================
        //  LỌC / TÌM KIẾM / THANH TRẠNG THÁI
        // =====================================================================
        private void ApplyFilter()
        {
            if (dgv == null || lblStatus == null) return;
            string q = Norm(txtSearch.Text);
            string st = cboFilterStatus.SelectedIndex > 0 ? cboFilterStatus.SelectedItem.ToString() : null;

            var result = _all.Where(s =>
                (q.Length == 0 || Norm(s.StudentId).Contains(q) || Norm(s.Equipment).Contains(q)) &&
                (st == null || s.Status == st)).ToList();

            var keep = _selected;
            _loading = true;
            _bs.DataSource = new BindingList<LoanSlip>(result);
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

            if (keep != null && idx < 0) EnterNewMode();   // phiếu đang sửa không còn trong danh sách lọc
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            int overdue = _all.Count(s => s.Status == LoanSlip.Overdue);
            lblStatus.Text = $"Tổng số phiếu: {_all.Count}  |  Quá hạn: {overdue}  |  Đang hiển thị: {_bs.Count}";
        }

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
            var s = dgv.SelectedRows[0].DataBoundItem as LoanSlip;
            if (s != null) LoadToForm(s);
        }

        private void LoadToForm(LoanSlip s)
        {
            _selected = s;
            txtId.Text = s.Id;
            txtId.ReadOnly = true;
            txtStudentId.Text = s.StudentId;
            txtStudentName.Text = s.StudentName;
            cboEquipment.SelectedItem = s.Equipment;
            nudQty.Value = Math.Min(Math.Max(s.Quantity, nudQty.Minimum), nudQty.Maximum);
            dtpBorrow.Value = s.BorrowDate;
            dtpDue.Value = s.DueDate;
            chkReturned.Checked = s.ReturnDate.HasValue;
            dtpReturn.Value = s.ReturnDate ?? DateTime.Today;
            _err.Clear();
            UpdateStatusPreview();
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
            txtStudentId.Text = "";
            txtStudentName.Text = "";
            cboEquipment.SelectedIndex = -1;
            nudQty.Value = 1;
            dtpBorrow.Value = DateTime.Today;
            dtpDue.Value = DateTime.Today.AddDays(7);
            chkReturned.Checked = false;
            dtpReturn.Value = DateTime.Today;
            _err.Clear();
            UpdateStatusPreview();
            SetModeButtons();
            txtStudentId.Focus();
        }

        private void SetModeButtons()
        {
            btnAdd.Enabled = _selected == null;
            btnUpdate.Enabled = _selected != null;
            btnDelete.Enabled = _selected != null;
            btnReturn.Enabled = _selected != null && !_selected.ReturnDate.HasValue;
        }

        private void UpdateStatusPreview()
        {
            if (txtStatus == null) return;
            txtStatus.Text = LoanSlip.ComputeStatus(
                chkReturned.Checked ? dtpReturn.Value.Date : (DateTime?)null, dtpDue.Value.Date);
        }

        private string NextId()
        {
            int n = _all.Count + 1;
            while (_all.Any(s => s.Id.Equals("PM" + n.ToString("D3"), StringComparison.OrdinalIgnoreCase))) n++;
            return "PM" + n.ToString("D3");
        }

        // =====================================================================
        //  THÊM / SỬA / XÓA / TRẢ
        // =====================================================================
        private void AddSlip()
        {
            if (!ValidateAll(true)) return;
            if (!Confirm("Thêm phiếu mượn này?")) return;

            var s = new LoanSlip();
            ReadForm(s);
            _all.Add(s);
            EnterNewMode();
            ApplyFilter();
        }

        private void UpdateSlip()
        {
            if (_selected == null) return;
            if (!ValidateAll(false)) return;
            if (!Confirm($"Cập nhật phiếu {_selected.Id}?")) return;

            ReadForm(_selected);
            ApplyFilter();
            if (_selected != null) LoadToForm(_selected);   // làm mới tình trạng/nút trên form
        }

        private void DeleteSlip()
        {
            if (_selected == null) return;
            if (!Confirm($"Xóa phiếu {_selected.Id} của sinh viên {_selected.StudentId}?")) return;

            _all.Remove(_selected);
            _selected = null;
            ApplyFilter();
            EnterNewMode();
        }

        private void MarkReturned()
        {
            if (_selected == null) return;
            if (_selected.ReturnDate.HasValue)
            {
                MessageBox.Show("Phiếu này đã được trả.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DateTime today = DateTime.Today;
            if (today < _selected.BorrowDate.Date)
            {
                MessageBox.Show("Ngày trả thực tế không được trước ngày mượn.", "Không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Confirm($"Đánh dấu phiếu {_selected.Id} đã trả vào {today:dd/MM/yyyy}?")) return;

            _selected.ReturnDate = today;
            ApplyFilter();                                   // cập nhật lưới + thanh trạng thái
            if (_selected != null) LoadToForm(_selected);    // cập nhật form (nếu dòng còn trong danh sách lọc)
        }

        private void ReadForm(LoanSlip s)
        {
            s.Id = txtId.Text.Trim();
            s.StudentId = txtStudentId.Text.Trim();
            s.StudentName = txtStudentName.Text.Trim();
            s.Equipment = cboEquipment.SelectedItem.ToString();
            s.Quantity = (int)nudQty.Value;
            s.BorrowDate = dtpBorrow.Value.Date;
            s.DueDate = dtpDue.Value.Date;
            s.ReturnDate = chkReturned.Checked ? dtpReturn.Value.Date : (DateTime?)null;
        }

        private bool Confirm(string msg) =>
            MessageBox.Show(msg, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        // =====================================================================
        //  KIỂM TRA DỮ LIỆU (ErrorProvider)
        // =====================================================================
        private bool ValidateAll(bool isNew) =>
            ValidateId(isNew) & ValidateStudentId() & ValidateStudentName() &
            ValidateEquipment() & ValidateQty() & ValidateDates() & ValidateReturn();

        private bool ValidateId(bool isNew)
        {
            string id = txtId.Text.Trim();
            if (id.Length == 0) return SetError(txtId, "Mã phiếu không được để trống");
            if (isNew && _all.Any(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                return SetError(txtId, "Mã phiếu đã tồn tại");
            return SetError(txtId, null);
        }

        private bool ValidateStudentId() =>
            SetError(txtStudentId, txtStudentId.Text.Trim().Length == 0 ? "Mã sinh viên không được để trống" : null);

        private bool ValidateStudentName() =>
            SetError(txtStudentName, txtStudentName.Text.Trim().Length == 0 ? "Vui lòng nhập họ tên sinh viên" : null);

        private bool ValidateEquipment() =>
            SetError(cboEquipment, cboEquipment.SelectedIndex < 0 ? "Vui lòng chọn thiết bị" : null);

        private bool ValidateQty() =>
            SetError(nudQty, nudQty.Value <= 0 ? "Số lượng phải lớn hơn 0" : null);

        private bool ValidateDates() =>
            SetError(dtpDue, dtpDue.Value.Date < dtpBorrow.Value.Date ? "Hạn trả không được trước ngày mượn" : null);

        private bool ValidateReturn()
        {
            if (!chkReturned.Checked) return SetError(dtpReturn, null);
            return SetError(dtpReturn, dtpReturn.Value.Date < dtpBorrow.Value.Date
                ? "Ngày trả thực tế không được trước ngày mượn" : null);
        }

        private bool SetError(Control c, string message)
        {
            _err.SetError(c, message ?? "");
            return message == null;
        }

        // =====================================================================
        //  XUẤT CSV (các phiếu đang lọc)
        // =====================================================================
        private void ExportCsv()
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"phieu_muon_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Xuất các phiếu đang hiển thị"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Mã phiếu,Mã sinh viên,Họ tên,Thiết bị,Số lượng,Ngày mượn,Hạn trả,Ngày trả thực tế,Tình trạng");
                foreach (LoanSlip s in _bs.List)
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        Csv(s.Id), Csv(s.StudentId), Csv(s.StudentName), Csv(s.Equipment),
                        s.Quantity.ToString(), s.BorrowDate.ToString("dd/MM/yyyy"), s.DueDate.ToString("dd/MM/yyyy"),
                        s.ReturnDateText, Csv(s.Status)
                    }));
                }
                try
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show($"Đã xuất {_bs.Count} phiếu.", "Xuất CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
