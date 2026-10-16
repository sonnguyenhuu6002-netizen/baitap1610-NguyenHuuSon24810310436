using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RepairServiceTracker
{
    public class MainForm : Form
    {
        private static readonly string[] DeviceTypes =
            { "Điện thoại", "Laptop", "Máy tính bảng", "Máy tính để bàn", "Máy in", "Tivi", "Thiết bị khác" };
        private static readonly string[] Statuses =
            { RepairOrder.StatusNew, RepairOrder.StatusRepairing, RepairOrder.StatusWaitingParts,
              RepairOrder.StatusDone, RepairOrder.StatusCancelled };

        private readonly BindingList<RepairOrder> _all = new BindingList<RepairOrder>();  // toàn bộ đơn
        private readonly BindingSource _bs = new BindingSource();                          // nguồn của lưới (đã lọc)
        private readonly ErrorProvider _err = new ErrorProvider();
        private RepairOrder _selected;      // null = chế độ thêm mới
        private string _currentImagePath;   // ảnh đang hiển thị trên form
        private bool _loading;

        private TextBox txtId, txtName, txtPhone, txtCost, txtDesc, txtSearch;
        private ComboBox cboDevice, cboStatus, cboFilterStatus;
        private DateTimePicker dtpReceived;
        private PictureBox picDevice;
        private Button btnNew, btnAdd, btnUpdate, btnDelete, btnPickImage, btnClearImage, btnClearFilter;
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
            Text = "Repair Service Tracker";
            Font = new Font("Segoe UI", 10F);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1200, 760);
            MinimumSize = new Size(1000, 660);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Hàng trên: thông tin đơn (co giãn) + ảnh thiết bị (cố định bề ngang)
            var top = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, RowCount = 1 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            top.Controls.Add(BuildInputGroup(), 0, 0);
            top.Controls.Add(BuildPictureGroup(), 1, 0);

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(BuildFilterGroup(), 0, 1);
            root.Controls.Add(BuildGrid(), 0, 2);

            var menu = new MenuStrip();
            var mFile = new ToolStripMenuItem("&Tệp");
            mFile.DropDownItems.Add(new ToolStripMenuItem("&Xuất danh sách đang hiển thị ra CSV...", null, (s, e) => ExportCsv())
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
            var grp = new GroupBox { Text = "Thông tin đơn sửa chữa", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 6 };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 6; i++) t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            txtId = new TextBox();
            txtName = new TextBox();
            txtPhone = new TextBox();
            txtCost = new TextBox { TextAlign = HorizontalAlignment.Right };
            txtDesc = new TextBox { Multiline = true, Height = 56, ScrollBars = ScrollBars.Vertical };
            dtpReceived = new DateTimePicker { Format = DateTimePickerFormat.Short };
            cboDevice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            cboDevice.Items.AddRange(DeviceTypes);
            cboStatus.Items.AddRange(Statuses);

            AddRow(t, 0, "Mã đơn:", txtId, "Ngày tiếp nhận:", dtpReceived);
            AddRow(t, 1, "Tên khách hàng:", txtName, "Số điện thoại:", txtPhone);
            AddRow(t, 2, "Loại thiết bị:", cboDevice, "Trạng thái:", cboStatus);
            AddRow(t, 3, "Chi phí dự kiến (đ):", txtCost, "", new Label());

            t.Controls.Add(MakeLabel("Mô tả lỗi:"), 0, 4);
            t.Controls.Add(txtDesc, 1, 4);
            t.SetColumnSpan(txtDesc, 3);
            txtDesc.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtDesc.Margin = new Padding(3, 4, 3, 4);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            btnNew = new Button { Text = "Mới", AutoSize = true };
            btnAdd = new Button { Text = "Thêm", AutoSize = true };
            btnUpdate = new Button { Text = "Cập nhật", AutoSize = true };
            btnDelete = new Button { Text = "Xóa", AutoSize = true };
            buttons.Controls.AddRange(new Control[] { btnNew, btnAdd, btnUpdate, btnDelete });
            t.Controls.Add(buttons, 0, 5);
            t.SetColumnSpan(buttons, 4);

            btnNew.Click += (s, e) => EnterNewMode();
            btnAdd.Click += (s, e) => AddOrder();
            btnUpdate.Click += (s, e) => UpdateOrder();
            btnDelete.Click += (s, e) => DeleteOrder();

            txtId.Leave += (s, e) => { if (!txtId.ReadOnly) ValidateId(true); };
            txtName.Leave += (s, e) => ValidateName();
            txtPhone.Leave += (s, e) => ValidatePhone();
            txtDesc.Leave += (s, e) => ValidateDesc();
            txtCost.Leave += (s, e) =>
            {
                if (ValidateCost() && TryParseCost(out decimal v)) txtCost.Text = v.ToString("N0");
            };

            grp.Controls.Add(t);
            return grp;
        }

        private Control BuildPictureGroup()
        {
            var grp = new GroupBox { Text = "Ảnh thiết bị", Dock = DockStyle.Fill, Padding = new Padding(8) };
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            picDevice = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,     // hiển thị đúng tỷ lệ, không méo
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.WhiteSmoke,
                MinimumSize = new Size(180, 150)
            };
            var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            btnPickImage = new Button { Text = "Chọn ảnh...", AutoSize = true };
            btnClearImage = new Button { Text = "Bỏ ảnh", AutoSize = true };
            flow.Controls.Add(btnPickImage);
            flow.Controls.Add(btnClearImage);

            btnPickImage.Click += (s, e) => PickImage();
            btnClearImage.Click += (s, e) => SetImage(null);

            t.Controls.Add(picDevice, 0, 0);
            t.Controls.Add(flow, 0, 1);
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
            cboFilterStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
            cboFilterStatus.Items.Add("Tất cả");
            cboFilterStatus.Items.AddRange(Statuses);
            cboFilterStatus.SelectedIndex = 0;
            btnClearFilter = new Button { Text = "Bỏ lọc", AutoSize = true };

            t.Controls.Add(MakeLabel("Tìm (mã đơn / tên KH / SĐT):"), 0, 0);
            t.Controls.Add(txtSearch, 1, 0);
            t.Controls.Add(MakeLabel("Trạng thái:"), 2, 0);
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
            dgv.Columns.Add(Col("Id", "Mã đơn", 70));
            dgv.Columns.Add(Col("CustomerName", "Khách hàng", 130));
            dgv.Columns.Add(Col("Phone", "Số điện thoại", 100));
            dgv.Columns.Add(Col("DeviceType", "Loại thiết bị", 110));
            dgv.Columns.Add(Col("Description", "Mô tả lỗi", 200));
            var colDate = Col("ReceivedDate", "Ngày tiếp nhận", 95);
            colDate.DefaultCellStyle.Format = "dd/MM/yyyy";
            dgv.Columns.Add(colDate);
            var colCost = Col("EstimatedCost", "Chi phí dự kiến", 110);
            colCost.DefaultCellStyle.Format = "N0";
            colCost.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            dgv.Columns.Add(colCost);
            dgv.Columns.Add(Col("Status", "Trạng thái", 100));

            dgv.DataSource = _bs;
            dgv.SelectionChanged += Dgv_SelectionChanged;
            dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || dgv.Columns[e.ColumnIndex].DataPropertyName != "Status") return;
                switch (e.Value as string)
                {
                    case RepairOrder.StatusRepairing: e.CellStyle.ForeColor = Color.RoyalBlue; break;
                    case RepairOrder.StatusWaitingParts: e.CellStyle.ForeColor = Color.DarkOrange; break;
                    case RepairOrder.StatusDone: e.CellStyle.ForeColor = Color.SeaGreen; break;
                    case RepairOrder.StatusCancelled: e.CellStyle.ForeColor = Color.Gray; break;
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
            _all.Add(new RepairOrder { Id = "DH001", CustomerName = "Nguyễn Văn An", Phone = "0912345678", DeviceType = "Điện thoại", Description = "Vỡ màn hình, thay màn mới", ReceivedDate = d.AddDays(-3), EstimatedCost = 1500000, Status = RepairOrder.StatusRepairing });
            _all.Add(new RepairOrder { Id = "DH002", CustomerName = "Trần Thị Bình", Phone = "0987654321", DeviceType = "Laptop", Description = "Không lên nguồn", ReceivedDate = d.AddDays(-2), EstimatedCost = 800000, Status = RepairOrder.StatusNew });
            _all.Add(new RepairOrder { Id = "DH003", CustomerName = "Lê Minh Châu", Phone = "0905111222", DeviceType = "Máy in", Description = "Kẹt giấy, hỏng trục kéo", ReceivedDate = d.AddDays(-6), EstimatedCost = 450000, Status = RepairOrder.StatusWaitingParts });
            _all.Add(new RepairOrder { Id = "DH004", CustomerName = "Phạm Quốc Dũng", Phone = "0333444555", DeviceType = "Tivi", Description = "Mất hình, còn tiếng", ReceivedDate = d.AddDays(-10), EstimatedCost = 2200000, Status = RepairOrder.StatusDone });
            _all.Add(new RepairOrder { Id = "DH005", CustomerName = "Hoàng Thu Hà", Phone = "0966777888", DeviceType = "Máy tính bảng", Description = "Chai pin, sạc không vào", ReceivedDate = d.AddDays(-1), EstimatedCost = 600000, Status = RepairOrder.StatusCancelled });
        }

        // =====================================================================
        //  LỌC / TÌM KIẾM / THANH TRẠNG THÁI
        // =====================================================================
        private void ApplyFilter()
        {
            if (dgv == null || lblStatus == null) return;
            string q = Norm(txtSearch.Text);
            string qPhone = txtSearch.Text.Replace(" ", "").Replace(".", "").Replace("-", "");
            string st = cboFilterStatus.SelectedIndex > 0 ? cboFilterStatus.SelectedItem.ToString() : null;

            var result = _all.Where(o =>
                (q.Length == 0 ||
                    Norm(o.Id).Contains(q) ||
                    Norm(o.CustomerName).Contains(q) ||
                    (qPhone.Length > 0 && o.Phone.Contains(qPhone))) &&
                (st == null || o.Status == st)).ToList();

            var keep = _selected;
            _loading = true;
            _bs.DataSource = new BindingList<RepairOrder>(result);
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

            if (keep != null && idx < 0) EnterNewMode();   // đơn đang sửa không còn trong danh sách lọc
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            decimal total = _all.Where(o => o.IsUnfinished).Sum(o => o.EstimatedCost);
            lblStatus.Text = $"Số đơn theo bộ lọc: {_bs.Count} / {_all.Count}   |   " +
                             $"Tổng chi phí dự kiến (đơn chưa hoàn tất): {total:N0} đ";
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
            var o = dgv.SelectedRows[0].DataBoundItem as RepairOrder;
            if (o != null) LoadToForm(o);
        }

        private void LoadToForm(RepairOrder o)
        {
            _selected = o;
            txtId.Text = o.Id;
            txtId.ReadOnly = true;
            txtName.Text = o.CustomerName;
            txtPhone.Text = o.Phone;
            cboDevice.SelectedItem = o.DeviceType;
            cboStatus.SelectedItem = o.Status;
            dtpReceived.Value = o.ReceivedDate;
            txtCost.Text = o.EstimatedCost.ToString("N0");
            txtDesc.Text = o.Description;
            SetImage(o.ImagePath);
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
            cboDevice.SelectedIndex = 0;
            cboStatus.SelectedIndex = 0;
            dtpReceived.Value = DateTime.Today;
            txtCost.Text = "0";
            txtDesc.Text = "";
            SetImage(null);
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
            while (_all.Any(o => o.Id.Equals("DH" + n.ToString("D3"), StringComparison.OrdinalIgnoreCase))) n++;
            return "DH" + n.ToString("D3");
        }

        // =====================================================================
        //  ẢNH THIẾT BỊ
        // =====================================================================
        private void PickImage()
        {
            using (var dlg = new OpenFileDialog
            {
                Title = "Chọn ảnh thiết bị",
                Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.bmp;*.gif)|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Tất cả tệp (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!SetImage(dlg.FileName))
                    MessageBox.Show("Không thể mở tệp này như một ảnh.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Nạp ảnh vào PictureBox (đọc xong đóng file ngay để không khóa tệp). Trả về false nếu lỗi.
        private bool SetImage(string path)
        {
            var old = picDevice.Image;
            picDevice.Image = null;
            old?.Dispose();

            if (string.IsNullOrEmpty(path))
            {
                _currentImagePath = null;
                return true;
            }
            try
            {
                using (var fs = File.OpenRead(path))
                using (var img = Image.FromStream(fs))
                {
                    picDevice.Image = new Bitmap(img);
                }
                _currentImagePath = path;
                return true;
            }
            catch
            {
                _currentImagePath = null;   // tệp không tồn tại / không phải ảnh
                return false;
            }
        }

        // =====================================================================
        //  THÊM / SỬA / XÓA
        // =====================================================================
        private void AddOrder()
        {
            if (!ValidateAll(true)) return;
            if (!Confirm("Thêm đơn sửa chữa này?")) return;

            var o = new RepairOrder();
            ReadForm(o);
            _all.Add(o);
            EnterNewMode();
            ApplyFilter();
        }

        private void UpdateOrder()
        {
            if (_selected == null) return;
            if (!ValidateAll(false)) return;
            if (!Confirm($"Cập nhật đơn {_selected.Id}?")) return;

            ReadForm(_selected);
            ApplyFilter();   // làm mới lưới + tổng chi phí trên thanh trạng thái
        }

        private void DeleteOrder()
        {
            if (_selected == null) return;
            if (!Confirm($"Xóa đơn {_selected.Id} của {_selected.CustomerName}?")) return;

            _all.Remove(_selected);
            _selected = null;
            ApplyFilter();
            EnterNewMode();
        }

        private void ReadForm(RepairOrder o)
        {
            TryParseCost(out decimal cost);
            o.Id = txtId.Text.Trim();
            o.CustomerName = txtName.Text.Trim();
            o.Phone = txtPhone.Text.Trim();
            o.DeviceType = cboDevice.SelectedItem.ToString();
            o.Description = txtDesc.Text.Trim();
            o.ReceivedDate = dtpReceived.Value.Date;
            o.EstimatedCost = cost;
            o.Status = cboStatus.SelectedItem.ToString();
            o.ImagePath = _currentImagePath;
        }

        private bool Confirm(string msg) =>
            MessageBox.Show(msg, "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

        // =====================================================================
        //  KIỂM TRA DỮ LIỆU (ErrorProvider)
        // =====================================================================
        private bool ValidateAll(bool isNew) =>
            ValidateId(isNew) & ValidateName() & ValidatePhone() & ValidateCost() & ValidateDesc();

        private bool ValidateId(bool isNew)
        {
            string id = txtId.Text.Trim();
            if (id.Length == 0) return SetError(txtId, "Mã đơn không được để trống");
            if (isNew && _all.Any(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                return SetError(txtId, "Mã đơn đã tồn tại");
            return SetError(txtId, null);
        }

        private bool ValidateName() =>
            SetError(txtName, txtName.Text.Trim().Length == 0 ? "Vui lòng nhập tên khách hàng" : null);

        private bool ValidatePhone() =>
            SetError(txtPhone, txtPhone.Text.Trim().Length == 0 ? "Vui lòng nhập số điện thoại" : null);

        private bool ValidateDesc() =>
            SetError(txtDesc, txtDesc.Text.Trim().Length == 0 ? "Vui lòng nhập mô tả lỗi" : null);

        private bool ValidateCost()
        {
            if (txtCost.Text.Trim().Length == 0)
                return SetError(txtCost, "Vui lòng nhập chi phí dự kiến (nhập 0 nếu chưa có)");
            if (!TryParseCost(out decimal v))
                return SetError(txtCost, "Chi phí phải là số (ví dụ: 1500000)");
            if (v < 0)
                return SetError(txtCost, "Chi phí dự kiến không được âm");
            return SetError(txtCost, null);
        }

        private bool TryParseCost(out decimal value) =>
            decimal.TryParse(txtCost.Text.Trim().Replace(" ", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out value);

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
                FileName = $"don_sua_chua_{DateTime.Now:yyyyMMdd_HHmm}.csv",
                Title = "Xuất danh sách đơn đang hiển thị"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var sb = new StringBuilder();
                sb.AppendLine("Mã đơn,Khách hàng,Số điện thoại,Loại thiết bị,Mô tả lỗi,Ngày tiếp nhận,Chi phí dự kiến,Trạng thái");
                foreach (RepairOrder o in _bs.List)
                {
                    sb.AppendLine(string.Join(",", new[]
                    {
                        Csv(o.Id), Csv(o.CustomerName), Csv(o.Phone), Csv(o.DeviceType), Csv(o.Description),
                        o.ReceivedDate.ToString("dd/MM/yyyy"),
                        o.EstimatedCost.ToString("0", CultureInfo.InvariantCulture),
                        Csv(o.Status)
                    }));
                }
                try
                {
                    File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                    MessageBox.Show($"Đã xuất {_bs.Count} đơn.", "Xuất CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
