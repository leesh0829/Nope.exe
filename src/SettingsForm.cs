using System.ComponentModel;
using System.Windows.Forms;

namespace NopeExe;

public sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly ConfigService _configService;
    private readonly ILogger _logger;

    private readonly NumericUpDown _pollIntervalInput;
    private readonly NumericUpDown _cooldownInput;
    private readonly DataGridView _rulesGrid;
    private readonly BindingList<RuleRow> _rows;

    public SettingsForm(AppConfig config, ConfigService configService, ILogger logger)
    {
        _config = config;
        _configService = configService;
        _logger = logger;

        Text = "Nope.exe 설정";
        Width = 1250;
        Height = 680;
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var generalPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };

        _pollIntervalInput = new NumericUpDown { Minimum = 100, Maximum = 10000, Value = Math.Clamp(_config.PollIntervalMs, 100, 10000), Width = 100 };
        _cooldownInput = new NumericUpDown { Minimum = 1, Maximum = 3600, Value = Math.Clamp(_config.CooldownSeconds, 1, 3600), Width = 100 };

        generalPanel.Controls.Add(new Label { Text = "PollInterval(ms)", AutoSize = true, Margin = new Padding(0, 8, 5, 0) });
        generalPanel.Controls.Add(_pollIntervalInput);
        generalPanel.Controls.Add(new Label { Text = "Cooldown(sec)", AutoSize = true, Margin = new Padding(12, 8, 5, 0) });
        generalPanel.Controls.Add(_cooldownInput);

        _rows = new BindingList<RuleRow>(_config.Rules.Select(RuleRow.FromRule).ToList());
        _rulesGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            DataSource = _rows,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        };

        _rulesGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(RuleRow.Enabled), HeaderText = "Enabled" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.Name), HeaderText = "Name", Width = 180 });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.ProcessNameExact), HeaderText = "ProcessNameExact" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.ProcessPathExact), HeaderText = "ProcessPathExact", Width = 240 });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.FileDescriptionContains), HeaderText = "FileDescriptionContains" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.CompanyNameContains), HeaderText = "CompanyNameContains" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.WindowTitleRegex), HeaderText = "WindowTitleRegex", Width = 180 });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.WindowClassExact), HeaderText = "WindowClassExact" });
        _rulesGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(RuleRow.ActionType),
            HeaderText = "Action",
            DataSource = Enum.GetValues<RuleActionType>(),
        });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.SendMessageTimeoutMs), HeaderText = "CloseTimeoutMs" });

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };

        var saveButton = new Button { Text = "저장", AutoSize = true };
        saveButton.Click += (_, _) => SaveAndClose();

        var applyButton = new Button { Text = "적용", AutoSize = true };
        applyButton.Click += (_, _) => SaveOnly();

        var cancelButton = new Button { Text = "취소", AutoSize = true };
        cancelButton.Click += (_, _) => Close();

        var addRuleButton = new Button { Text = "규칙 추가", AutoSize = true };
        addRuleButton.Click += (_, _) => _rows.Add(new RuleRow());

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(applyButton);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(addRuleButton);

        root.Controls.Add(generalPanel, 0, 0);
        root.Controls.Add(_rulesGrid, 0, 1);
        root.Controls.Add(buttonPanel, 0, 2);

        Controls.Add(root);
    }

    private void SaveAndClose()
    {
        if (!ApplyToConfig())
        {
            return;
        }

        Close();
    }

    private void SaveOnly() => ApplyToConfig();

    private bool ApplyToConfig()
    {
        try
        {
            _rulesGrid.EndEdit();

            var nextRules = new List<RuleConfig>();
            foreach (var row in _rows)
            {
                if (string.IsNullOrWhiteSpace(row.Name) &&
                    string.IsNullOrWhiteSpace(row.ProcessNameExact) &&
                    string.IsNullOrWhiteSpace(row.ProcessPathExact) &&
                    string.IsNullOrWhiteSpace(row.FileDescriptionContains) &&
                    string.IsNullOrWhiteSpace(row.CompanyNameContains) &&
                    string.IsNullOrWhiteSpace(row.WindowTitleRegex) &&
                    string.IsNullOrWhiteSpace(row.WindowClassExact))
                {
                    continue;
                }

                var rule = row.ToRule();
                if (!rule.CompileRegex(out var error))
                {
                    MessageBox.Show($"규칙 '{rule.Name}' 의 정규식이 잘못되었습니다: {error}", "정규식 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                nextRules.Add(rule);
            }

            _config.PollIntervalMs = (int)_pollIntervalInput.Value;
            _config.CooldownSeconds = (int)_cooldownInput.Value;
            _config.Rules = nextRules;

            _configService.Save(_config);
            _logger.Info("Settings updated via UI and saved.");
            MessageBox.Show("설정이 저장되었습니다.", "Nope.exe", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _logger.Error($"Settings save failed: {ex}");
            return false;
        }
    }

    private sealed class RuleRow
    {
        public string Name { get; set; } = "New Rule";
        public bool Enabled { get; set; } = true;
        public string? ProcessNameExact { get; set; }
        public string? ProcessPathExact { get; set; }
        public string? FileDescriptionContains { get; set; }
        public string? CompanyNameContains { get; set; }
        public string? WindowTitleRegex { get; set; }
        public string? WindowClassExact { get; set; }
        public RuleActionType ActionType { get; set; } = RuleActionType.Close;
        public uint SendMessageTimeoutMs { get; set; } = 1500;

        public RuleConfig ToRule() => new()
        {
            Name = string.IsNullOrWhiteSpace(Name) ? "Unnamed rule" : Name,
            Enabled = Enabled,
            Match = new MatchConfig
            {
                ProcessNameExact = NullIfWhitespace(ProcessNameExact),
                ProcessPathExact = NullIfWhitespace(ProcessPathExact),
                FileDescriptionContains = NullIfWhitespace(FileDescriptionContains),
                CompanyNameContains = NullIfWhitespace(CompanyNameContains),
                WindowTitleRegex = NullIfWhitespace(WindowTitleRegex),
                WindowClassExact = NullIfWhitespace(WindowClassExact),
            },
            Action = new ActionConfig
            {
                Type = ActionType,
                SendMessageTimeoutMs = SendMessageTimeoutMs == 0 ? 1500u : SendMessageTimeoutMs,
            },
        };

        public static RuleRow FromRule(RuleConfig rule) => new()
        {
            Name = rule.Name,
            Enabled = rule.Enabled,
            ProcessNameExact = rule.Match.ProcessNameExact,
            ProcessPathExact = rule.Match.ProcessPathExact,
            FileDescriptionContains = rule.Match.FileDescriptionContains,
            CompanyNameContains = rule.Match.CompanyNameContains,
            WindowTitleRegex = rule.Match.WindowTitleRegex,
            WindowClassExact = rule.Match.WindowClassExact,
            ActionType = rule.Action.Type,
            SendMessageTimeoutMs = rule.Action.SendMessageTimeoutMs,
        };

        private static string? NullIfWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
