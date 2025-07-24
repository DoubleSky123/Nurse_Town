using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("UI References")]
    public Canvas evaluationCanvas;
    public TextMeshProUGUI reportText;  // 使用TextMeshPro
    public Button closeButton;

    private string scoringPrompt = "";
    private string currentScenario = "";
    private List<ConversationTurn> conversationTurns = new List<ConversationTurn>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // 初始化时隐藏评估界面
        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(false);

        // 绑定关闭按钮事件
        if (closeButton != null)
            closeButton.onClick.AddListener(HideEvaluationPanel);
    }

    void Update()
    {
        // 移除E键监听，现在通过CameraClipboardController直接调用
    }

    public void Initialize(string scenario)
    {
        currentScenario = scenario;
        LoadScoringPrompt();
    }

    private void LoadScoringPrompt()
    {
        string promptPath = Path.Combine(Application.streamingAssetsPath, "Prompts", currentScenario, "scoringPrompt.txt");
        if (!File.Exists(promptPath))
        {
            Debug.LogError("Scoring prompt file not found: " + promptPath);
            scoringPrompt = "";
            return;
        }
        scoringPrompt = File.ReadAllText(promptPath);
        Debug.Log("Scoring prompt loaded successfully.");
    }

    public void RecordTurn(string patientResponse, string nurseResponse)
    {
        conversationTurns.Add(new ConversationTurn
        {
            Patient = patientResponse,
            Nurse = nurseResponse
        });
        Debug.Log($"Turn {conversationTurns.Count} recorded.");
    }

    public void SubmitEvaluation()
    {
        if (conversationTurns.Count == 0)
        {
            Debug.LogWarning("No conversation turns recorded.");
            return;
        }
        StartCoroutine(EvaluateFullConversationCoroutine());
    }

    private IEnumerator EvaluateFullConversationCoroutine()
    {
        if (string.IsNullOrEmpty(scoringPrompt))
        {
            Debug.LogWarning("Scoring prompt not loaded.");
            yield break;
        }

        StringBuilder conversationBuilder = new StringBuilder();
        for (int i = 0; i < conversationTurns.Count; i++)
        {
            conversationBuilder.AppendLine($"Turn {i + 1}:");
            conversationBuilder.AppendLine($"Patient: \"{conversationTurns[i].Patient}\"");
            conversationBuilder.AppendLine($"Nursing Student: \"{conversationTurns[i].Nurse}\"");
            conversationBuilder.AppendLine();
        }

        string fullPrompt = $"{scoringPrompt}\n\nNow analyze the following full simulated conversation between the patient and nursing student:\n{conversationBuilder}";
        Debug.Log($"Prompt length: {fullPrompt.Length} characters");
        Debug.Log($"Estimated tokens (rough): {fullPrompt.Length / 4} tokens");

        var requestBody = new
        {
            model = "gpt-4",
            messages = new List<Dictionary<string, string>>()
            {
                new Dictionary<string, string>() { { "role", "user" }, { "content", fullPrompt } }
            },
            temperature = 0.0,
            max_tokens = 1500
        };

        string jsonBody = JsonConvert.SerializeObject(requestBody);

        var request = new UnityWebRequest(OpenAIRequest.Instance.apiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + OpenAIRequest.Instance.apiKey);

        Debug.Log("Submitting full conversation for evaluation...");
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("OpenAI Request Error: " + request.error);
            yield break;
        }

        var jsonResponse = JObject.Parse(request.downloadHandler.text);
        string responseContent = jsonResponse["choices"][0]["message"]["content"].ToString();

        try
        {
            var evaluation = JsonConvert.DeserializeObject<DynamicEvaluationResult>(responseContent);

            // 同时显示到Console和UI界面
            DisplayEvaluationToConsole(evaluation);
            DisplayEvaluationToUI(evaluation);
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to parse evaluation JSON: " + ex.Message);
        }
    }

    private void DisplayEvaluationToConsole(DynamicEvaluationResult evaluation)
    {
        Debug.Log("===== FINAL EVALUATION =====");
        foreach (var criterion in evaluation.criteria)
        {
            Debug.Log($"[{criterion.name}] Score: {criterion.score}/{criterion.maxScore} — {criterion.explanation}");
        }
        Debug.Log($"Total Score: {evaluation.totalScore}");
        Debug.Log($"Performance Level: {evaluation.performanceLevel}");
        Debug.Log($"Overall Summary: {evaluation.overallExplanation}");
    }

    private void DisplayEvaluationToUI(DynamicEvaluationResult evaluation)
    {
        // 显示评估界面
        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(true);

        // 构建报告文本内容（使用英文）
        StringBuilder reportContent = new StringBuilder();

        reportContent.AppendLine("Evaluation Report");
        reportContent.AppendLine();

        // 添加各项评估标准
        reportContent.AppendLine("Assessment Criteria Details");
        reportContent.AppendLine();
        foreach (var criterion in evaluation.criteria)
        {
            reportContent.AppendLine($"• {criterion.name}");
            reportContent.AppendLine($"  Score: {criterion.score}/{criterion.maxScore}");
            reportContent.AppendLine($"  Explanation: {criterion.explanation}");
            reportContent.AppendLine();
        }

        reportContent.AppendLine("Overall Assessment");
        reportContent.AppendLine($"Total Score: {evaluation.totalScore}");
        reportContent.AppendLine($"Performance Level: {evaluation.performanceLevel}");
        reportContent.AppendLine();
        reportContent.AppendLine("Overall Summary");
        reportContent.AppendLine(evaluation.overallExplanation);

        // 设置到TextMeshPro组件中
        if (reportText != null)
        {
            reportText.text = reportContent.ToString();

            // 强制刷新布局系统
            StartCoroutine(RefreshScrollViewLayout());
        }
    }

    private IEnumerator RefreshScrollViewLayout()
    {
        // 等待一帧让文本内容更新
        yield return null;

        // 强制更新Canvas布局
        Canvas.ForceUpdateCanvases();

        // 刷新Content Size Fitter
        var contentSizeFitter = reportText.GetComponent<UnityEngine.UI.ContentSizeFitter>();
        if (contentSizeFitter != null)
        {
            contentSizeFitter.SetLayoutVertical();
        }

        // 如果Content也有Content Size Fitter，也刷新它
        Transform contentParent = reportText.transform.parent;
        if (contentParent != null)
        {
            var parentContentSizeFitter = contentParent.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (parentContentSizeFitter != null)
            {
                parentContentSizeFitter.SetLayoutVertical();
            }
        }

        // 再次强制更新
        Canvas.ForceUpdateCanvases();

        // 重置滚动位置到顶部
        ScrollRect scrollRect = reportText.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f; // 1 = 顶部, 0 = 底部

            // 调试信息
            Debug.Log($"ScrollRect找到了！Content高度: {scrollRect.content.rect.height}");
            Debug.Log($"Viewport高度: {scrollRect.viewport.rect.height}");
            Debug.Log($"可滚动: {scrollRect.content.rect.height > scrollRect.viewport.rect.height}");
        }
        else
        {
            Debug.LogError("找不到ScrollRect组件！");
        }
    }

    public void HideEvaluationPanel()
    {
        if (evaluationCanvas != null)
            evaluationCanvas.gameObject.SetActive(false);
    }

    // 公开方法：供其他脚本调用
    public int GetConversationCount()
    {
        return conversationTurns.Count;
    }

    public bool CanViewReport()
    {
        return conversationTurns.Count >= 5;
    }
}

[Serializable]
public class ConversationTurn
{
    public string Patient;
    public string Nurse;
}

[Serializable]
public class DynamicEvaluationResult
{
    public List<CriterionScore> criteria;
    public int totalScore;
    public string performanceLevel;
    public string overallExplanation;
}

[Serializable]
public class CriterionScore
{
    public string name;
    public int score;
    public int maxScore;
    public string explanation;
}