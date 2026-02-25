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
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var generalPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
        };

        var pollLabel = new Label { Text = "PollInterval(ms)", AutoSize = true, Margin = new Padding(0, 8, 5, 0) };
        _pollIntervalInput = new NumericUpDown { Minimum = 100, Maximum = 10000, Value = Math.Clamp(_config.PollIntervalMs, 100, 10000), Width = 100 };

        var cooldownLabel = new Label { Text = "Cooldown(sec)", AutoSize = true, Margin = new Padding(12, 8, 5, 0) };
        _cooldownInput = new NumericUpDown { Minimum = 1, Maximum = 3600, Value = Math.Clamp(_config.CooldownSeconds, 1, 3600), Width = 100 };

        generalPanel.Controls.Add(pollLabel);
        generalPanel.Controls.Add(_pollIntervalInput);
        generalPanel.Controls.Add(cooldownLabel);
        generalPanel.Controls.Add(_cooldownInput);

        var helpTip = new ToolTip();
        helpTip.SetToolTip(pollLabel, "창 목록을 다시 검사하는 주기(밀리초). 낮을수록 반응은 빠르지만 CPU 사용량이 늘어납니다.");
        helpTip.SetToolTip(_pollIntervalInput, "권장: 500~1000ms");
        helpTip.SetToolTip(cooldownLabel, "같은 창/같은 규칙에 액션을 다시 적용하기 전 대기 시간(초).");
        helpTip.SetToolTip(_cooldownInput, "반복 처리 방지용 쿨다운");

        var guideLabel = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 8, 0, 8),
            Text =
                "빠른 설정: 아래에서 규칙 행 선택 → '프로세스 파일 선택' 버튼 클릭 → exe 선택하면 이름/경로가 자동 입력됩니다.\n" +
                "규칙 설명: 모든 match 조건은 AND(모두 만족)로 평가되며, 빈 칸은 무시됩니다.\n" +
                "- ProcessNameExact: 프로세스 이름 정확히 일치 (예: notepad)\n" +
                "- ProcessPathExact: 실행 파일 전체 경로 정확히 일치\n" +
                "- FileDescriptionContains / CompanyNameContains: 파일 메타데이터 부분 포함\n" +
                "- WindowTitleRegex: 창 제목 정규식 (대소문자 무시)\n" +
                "- WindowClassExact: 창 클래스명 정확히 일치\n" +
                "- Action: Close(정상 닫기), Minimize(최소화), Hide(숨김), KillProcess(강제 종료)\n" +
                "- CloseTimeoutMs: Close 동작 시 WM_CLOSE 응답 대기 시간(ms)",
        };

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

        _rulesGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(RuleRow.Enabled), HeaderText = "Enabled", ToolTipText = "체크 시 이 규칙 사용" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.Name), HeaderText = "Name", Width = 180, ToolTipText = "규칙 표시 이름" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.ProcessNameExact), HeaderText = "ProcessNameExact", ToolTipText = "프로세스 이름 정확히 일치" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.ProcessPathExact), HeaderText = "ProcessPathExact", Width = 240, ToolTipText = "실행 파일 경로 정확히 일치" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.FileDescriptionContains), HeaderText = "FileDescriptionContains", ToolTipText = "파일 설명 문자열 포함" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.CompanyNameContains), HeaderText = "CompanyNameContains", ToolTipText = "회사명 문자열 포함" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.WindowTitleRegex), HeaderText = "WindowTitleRegex", Width = 180, ToolTipText = "창 제목 정규식" });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.WindowClassExact), HeaderText = "WindowClassExact", ToolTipText = "창 클래스명 정확히 일치" });
        _rulesGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            DataPropertyName = nameof(RuleRow.ActionType),
            HeaderText = "Action",
            DataSource = Enum.GetValues<RuleActionType>(),
            ToolTipText = "매칭 시 실행할 동작",
        });
        _rulesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(RuleRow.SendMessageTimeoutMs), HeaderText = "CloseTimeoutMs", ToolTipText = "Close 액션의 응답 대기 시간(ms)" });

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

        var pickProcessButton = new Button { Text = "프로세스 파일 선택", AutoSize = true };
        pickProcessButton.Click += (_, _) => PickProcessFileForSelectedRule();

        var clearAdvancedButton = new Button { Text = "선택 규칙 고급조건 비우기", AutoSize = true };
        clearAdvancedButton.Click += (_, _) => ClearAdvancedConditionsForSelectedRule();

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(applyButton);
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(addRuleButton);
        buttonPanel.Controls.Add(clearAdvancedButton);
        buttonPanel.Controls.Add(pickProcessButton);

        root.Controls.Add(generalPanel, 0, 0);
        root.Controls.Add(guideLabel, 0, 1);
        root.Controls.Add(_rulesGrid, 0, 2);
        root.Controls.Add(buttonPanel, 0, 3);

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

    private void PickProcessFileForSelectedRule()
    {
        _rulesGrid.EndEdit();

        var row = GetSelectedRuleRow();
        if (row is null)
        {
            MessageBox.Show("먼저 규칙 행 하나를 선택하세요.", "Nope.exe", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string? selectedPath;
        try
        {
            selectedPath = SelectExecutableFileWithStaSupport(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"프로세스 파일 선택 중 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _logger.Error($"Process file picker failed: {ex}");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(selectedPath);

        row.ProcessPathExact = selectedPath;
        row.ProcessNameExact = processName;
        if (string.IsNullOrWhiteSpace(row.Name) || row.Name == "New Rule")
        {
            row.Name = $"{processName} rule";
        }

        _rulesGrid.Refresh();
    }

    private void ClearAdvancedConditionsForSelectedRule()
    {
        _rulesGrid.EndEdit();

        var row = GetSelectedRuleRow();
        if (row is null)
        {
            MessageBox.Show("먼저 규칙 행 하나를 선택하세요.", "Nope.exe", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        row.FileDescriptionContains = null;
        row.CompanyNameContains = null;
        row.WindowTitleRegex = null;
        row.WindowClassExact = null;
        _rulesGrid.Refresh();
    }

    private RuleRow? GetSelectedRuleRow()
    {
        if (_rulesGrid.CurrentRow?.DataBoundItem is RuleRow current)
        {
            return current;
        }

        var selected = _rulesGrid.SelectedRows.Cast<DataGridViewRow>()
            .Select(r => r.DataBoundItem)
            .OfType<RuleRow>()
            .FirstOrDefault();
        return selected;
    }

    private static string? SelectExecutableFileWithStaSupport(IWin32Window? owner)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return ShowExecutableDialog(owner);
        }

        string? selectedPath = null;
        Exception? dialogError = null;
        using var completed = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                selectedPath = ShowExecutableDialog(null);
            }
            catch (Exception ex)
            {
                dialogError = ex;
            }
            finally
            {
                completed.Set();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        completed.Wait();

        if (dialogError is not null)
        {
            throw new InvalidOperationException("프로세스 파일 선택 창을 여는 중 오류가 발생했습니다.", dialogError);
        }

        return selectedPath;
    }

    private static string? ShowExecutableDialog(IWin32Window? owner)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "프로세스 실행 파일 선택",
            Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };

        var result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return result == DialogResult.OK ? dialog.FileName : null;
    }

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
