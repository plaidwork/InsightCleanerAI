using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using InsightCleanerAI.Resources;
using InsightCleanerAI.ViewModels;
using WinForms = System.Windows.Forms;

namespace InsightCleanerAI
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null || sender is not PasswordBox passwordBox)
            {
                return;
            }

            ViewModel.CloudApiKey = passwordBox.Password;
        }

        private void SearchApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null || sender is not PasswordBox passwordBox)
            {
                return;
            }

            ViewModel.SearchApiKey = passwordBox.Password;
        }

        private void LocalLlmApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null || sender is not PasswordBox passwordBox)
            {
                return;
            }

            ViewModel.LocalLlmApiKey = passwordBox.Password;
        }

        private void MaxNodesButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ApplyMaxNodesPreset();
        }

        private void BrowseCacheButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            using var dialog = new WinForms.FolderBrowserDialog
            {
                Description = Strings.BrowseCacheDialogDescription,
                SelectedPath = Directory.Exists(ViewModel.CacheDirectory)
                    ? ViewModel.CacheDirectory
                    : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                ViewModel.CacheDirectory = dialog.SelectedPath;
            }
        }

        private void BrowseDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = Strings.BrowseDatabaseDialogTitle,
                FileName = Path.GetFileName(ViewModel.DatabasePath),
                InitialDirectory = GetDatabaseDirectory(),
                Filter = Strings.DatabaseFileFilter,
                AddExtension = true,
                DefaultExt = ".db"
            };

            if (dialog.ShowDialog() == true)
            {
                ViewModel.DatabasePath = dialog.FileName;
            }
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                Strings.ClearCacheConfirm,
                Strings.ClearCacheTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            if (ViewModel.TryClearCache(out var message))
            {
                MessageBox.Show(message, Strings.ClearCacheDoneTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(message, Strings.ClearCacheErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            Infrastructure.DebugLog.Info($"保存为默认设置 - AiMode={ViewModel.SelectedAiMode}, LocalLlmModel={ViewModel.LocalLlmModel}, CloudModel={ViewModel.CloudModel}");
            ViewModel.SaveConfiguration(includeSensitive: false);
            Infrastructure.DebugLog.Info("默认设置已保存");
            MessageBox.Show(
                Strings.SaveDefaultsMessage,
                Strings.ClearCacheDoneTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is not null)
            {
                Infrastructure.DebugLog.Info($"设置窗口关闭 - 保存配置前 AiMode={ViewModel.SelectedAiMode}, LocalLlmModel={ViewModel.LocalLlmModel}");
                ViewModel.SaveConfiguration();
                Infrastructure.DebugLog.Info("设置已保存");
            }
            Close();
        }

        private async void FetchCloudModelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.RemoteServerUrl))
            {
                MessageBox.Show(
                    "请先填写云端服务地址",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await ViewModel.LoadCloudModelsAsync();

            if (ViewModel.CloudModels.Count == 0)
            {
                MessageBox.Show(
                    "未能获取到模型列表，请检查：\n1. 服务地址是否正确\n2. API Key是否有效\n3. 网络连接是否正常",
                    "获取失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    $"成功获取 {ViewModel.CloudModels.Count} 个模型",
                    "获取成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async void FetchLocalModelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.LocalLlmEndpoint))
            {
                MessageBox.Show(
                    "请先填写本地LLM服务地址",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await ViewModel.LoadLocalModelsAsync();

            if (ViewModel.LocalModels.Count == 0)
            {
                MessageBox.Show(
                    "未能获取到模型列表，请检查：\n1. 本地LLM服务是否已启动\n2. 服务地址是否正确\n3. 是否支持模型列表接口",
                    "获取失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    $"成功获取 {ViewModel.LocalModels.Count} 个模型",
                    "获取成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private string GetDatabaseDirectory()
        {
            if (ViewModel is null)
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            var directory = Path.GetDirectoryName(ViewModel.DatabasePath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        // 帮助按钮点击事件处理
        private void ShowHelp(string title, string message)
        {
            MessageBox.Show(message, $"帮助 - {title}", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HelpButton_PrivacyMode_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("隐私模式", "• 公开模式：向AI发送真实完整路径\n  适用：本地AI（Ollama），数据不会上传互联网\n\n• 脱敏模式：隐藏真实路径，只显示相对结构\n  适用：云端AI，保护隐私信息");

        private void HelpButton_AiMode_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("AI模式", "• 关闭：仅显示树状结构，不生成AI说明\n• 离线规则：使用内置启发式分类器（最快，不联网）\n• 本地 LLM 服务：调用本地大模型（Ollama、koboldcpp等）\n• 云端搜索：调用云端AI接口（OpenAI、DeepSeek、百度千帆等）");

        private void HelpButton_InquiryScope_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("询问范围", "• 单个文件夹（不含子项）：最快，AI只能靠文件夹名猜测\n• 单层文件夹（附带子项列表）：准确度高，AI能看到内部结构\n• 所有文件逐一询问：最详细，但非常慢");

        private void HelpButton_CloudApiKey_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("云端API Key", "访问云端AI服务需要的密钥。在AI服务商网站注册账号后获得。\n\n默认不保存到配置文件，除非勾选\"保留API Key\"。");

        private void HelpButton_CloudEndpoint_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("云端服务地址", "AI服务的HTTP接口地址。\n\n常见服务：\n• OpenAI: api.openai.com/v1/chat/completions\n• DeepSeek: api.deepseek.com/v1/chat/completions");

        private void HelpButton_CloudModel_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("云端模型", "要使用的AI模型名称。\n\n点击\"获取模型\"按钮自动获取可用模型列表，或手动输入模型名。\n\n常见模型：gpt-4、gpt-3.5-turbo、deepseek-chat");

        private void HelpButton_CloudRequestTimeout_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("云端请求超时", "等待云端AI响应的最长时间（秒）。\n\n默认值：30秒\n建议：小模型20-30秒，大模型60-120秒");

        private void HelpButton_CloudConcurrency_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("云端并发限制", "同时向云端AI发送的最大请求数。\n\n默认值：2\n\n值越大扫描越快但可能触发API限流，建议1-3。");

        private void HelpButton_AiBatchSize_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("AI批量大小", "一次性处理多少个文件后暂停。\n\n默认值：1000\n\n防止一次性发送太多请求导致API限制或内存溢出。");

        private void HelpButton_AiTotalLimit_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("AI总结点数", "整个扫描过程中，最多给多少个文件生成AI说明。\n\n默认值：2000\n\n达到上限后剩余文件显示\"尚未生成说明\"。用于控制成本和时间。");

        private void HelpButton_SearchApiEndpoint_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("搜索API地址", "第三方搜索引擎API的地址（可选）。\n\n当AI不确定文件用途时，先搜索互联网获取背景知识，再生成说明。\n\n留空则不使用搜索功能。");

        private void HelpButton_SearchApiKey_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("搜索API Key", "访问搜索API需要的密钥（可选）。\n\n在搜索服务商网站注册账号后获得。");

        private void HelpButton_LocalLlmEndpoint_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("本地LLM服务地址", "本地大模型服务的HTTP接口地址。\n\n常见服务：\n• Ollama: http://127.0.0.1:11434/api/generate\n• koboldcpp: http://127.0.0.1:5001/api/v1/generate\n\n需先启动本地LLM服务。");

        private void HelpButton_LocalLlmModel_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("本地模型名称", "要使用的本地大模型名称。\n\n点击\"获取模型\"按钮自动获取已安装的模型列表。\n\n常见模型：qwen2:7b（快速）、gemma3:27b（准确）");

        private void HelpButton_LocalLlmApiKey_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("本地LLM API Key", "某些本地LLM服务需要的访问密钥（可选）。\n\nOllama默认不需要API Key，留空即可。");

        private void HelpButton_MaxDepth_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("最大扫描深度", "从根目录开始，最多扫描多少层子目录。\n\n默认值：5\n\n防止扫描过深导致耗时过长。");

        private void HelpButton_MaxNodes_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("最大节点数", "整个扫描过程中，最多扫描多少个文件和文件夹。\n\n默认值：20000\n\n防止意外扫描超大目录导致程序卡死。点击\"不限制\"按钮可取消限制。");

        private void HelpButton_DelayPerNode_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("扫描延迟", "扫描每个文件后等待的毫秒数。\n\n默认值：5毫秒\n\n防止磁盘IO占用过高。0=全速（SSD适用），10-50=温和（HDD适用）");

        private void HelpButton_CacheDirectory_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("缓存目录", "存储临时文件的目录。\n\n默认位置：%AppData%\\InsightCleanerAI\\cache\n\n删除此目录不会影响AI分析结果（AI结果存储在数据库中）。");

        private void HelpButton_DatabasePath_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("数据库路径", "存储AI生成说明的SQLite数据库文件。\n\n默认位置：%AppData%\\InsightCleanerAI\\insights.db\n\n避免重复分析相同文件。点击\"清空缓存\"可删除所有AI分析结果。");

        private void HelpButton_IgnoreCacheSize_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("缓存匹配模式", "控制如何判断\"文件是否已分析过\"。\n\n• 关闭：路径+文件大小都必须一致（严格匹配）\n• 开启：只看路径，忽略文件大小变化\n\n系统目录建议开启，工作目录建议关闭。");

        private void HelpButton_PersistApiKeys_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("保留API Key", "是否将API Key保存到配置文件。\n\n• 不勾选：每次启动需重新输入（更安全）\n• 勾选：下次启动自动填充（更方便）\n\n建议本地使用可勾选，多人共享电脑不建议。");

        private void HelpButton_IncludeHidden_Click(object sender, RoutedEventArgs e) =>
            ShowHelp("包含隐藏文件", "是否扫描Windows隐藏文件和文件夹。\n\n• 不勾选：跳过隐藏文件（.git、AppData等），扫描更快\n• 勾选：扫描所有文件，包括隐藏文件\n\n普通清理不建议勾选。");
    }
}

