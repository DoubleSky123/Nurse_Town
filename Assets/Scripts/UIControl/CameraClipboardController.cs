using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraClipboardController : MonoBehaviour
{
    [Header("Camera References")]
    public Camera mainCamera;              // 主摄像机
    public Transform clipboardViewPosition; // clipboard查看位置

    [Header("Original Camera Settings")]
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isViewingClipboard = false;

    [Header("Animation Settings")]
    public float transitionDuration = 2f;   // 切换动画时长
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("UI References")]
    public GameObject instructionText;      // "Press Enter to go to the report." 提示文本
    public GameObject clipboardReport;      // clipboard报告对象
    public GameObject subtitleUI;           // 字幕UI对象

    private static CameraClipboardController instance;
    public static CameraClipboardController Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<CameraClipboardController>();
            return instance;
        }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("CameraClipboardController Start方法执行了");

        // 保存原始摄像机位置和旋转
        if (mainCamera == null)
            mainCamera = Camera.main;

        originalPosition = mainCamera.transform.position;
        originalRotation = mainCamera.transform.rotation;

        Debug.Log($"保存的原始摄像机位置: {originalPosition}");

        // 初始状态下隐藏clipboard报告
        if (clipboardReport != null)
            clipboardReport.SetActive(false);

        // 初始状态下隐藏"Press Enter"提示文本，等待条件满足
        UpdateInstructionTextVisibility();
    }

    void Update()
    {
        // 检查时间缩放
        if (Time.timeScale == 0)
        {
            Debug.Log("游戏被暂停了，Time.timeScale = 0");
            return;
        }

        // 添加持续的心跳检测
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log($"T键按下，Time.timeScale = {Time.timeScale}");
        }

        // 持续检查并更新提示文本的显示状态
        UpdateInstructionTextVisibility();

        // 检测Enter键输入
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log("检测到Enter键按下");

            if (!isViewingClipboard)
            {
                Debug.Log("当前不在clipboard视角");

                // 使用ScoreManager检查状态
                ScoreManager scoreManager = ScoreManager.Instance;
                if (scoreManager == null)
                {
                    scoreManager = FindObjectOfType<ScoreManager>();
                }

                if (scoreManager == null)
                {
                    Debug.LogError("找不到ScoreManager脚本");
                }
                else
                {
                    Debug.Log($"找到ScoreManager，ConversationCount: {GetConversationCount(scoreManager)}");

                    // 检查是否有足够的对话轮次（5轮）
                    if (CanViewReport(scoreManager))
                    {
                        Debug.Log("对话完成，条件满足，开始切换摄像机到clipboard视角并直接生成评估报告");
                        // 切换视角的同时启动评估
                        StartCoroutine(SwitchToClipboardAndEvaluate(scoreManager));
                    }
                    else
                    {
                        int remaining = 5 - GetConversationCount(scoreManager);
                        Debug.Log($"对话不足，还需要 {remaining} 轮对话才能查看报告");
                    }
                }
            }
            else
            {
                Debug.Log("当前在clipboard视角");
            }
        }

        // 在clipboard视图时，按ESC返回
        if (Input.GetKeyDown(KeyCode.Escape) && isViewingClipboard)
        {
            Debug.Log("检测到ESC键，返回原视角");
            ReturnToOriginalView();
        }
    }

    // 获取对话轮次的方法
    private int GetConversationCount(ScoreManager scoreManager)
    {
        return scoreManager.GetConversationCount();
    }

    // 检查是否可以查看报告的方法
    private bool CanViewReport(ScoreManager scoreManager)
    {
        return scoreManager.CanViewReport();
    }

    // 新方法：切换视角的同时启动评估
    private IEnumerator SwitchToClipboardAndEvaluate(ScoreManager scoreManager)
    {
        if (isViewingClipboard || clipboardViewPosition == null)
        {
            Debug.Log("无法切换：已在clipboard视角或clipboardViewPosition为空");
            yield break;
        }

        Debug.Log("开始切换到clipboard视角并启动评估");

        isViewingClipboard = true;

        // 隐藏"Press Enter"提示文本
        if (instructionText != null)
        {
            instructionText.SetActive(false);
            Debug.Log("隐藏'Press Enter'提示文本");
        }

        // 隐藏字幕UI（强制隐藏所有子对象）
        if (subtitleUI != null)
        {
            HideSubtitleCompletely(subtitleUI);
            Debug.Log("隐藏字幕UI及其所有子对象");
        }

        // 摄像机平滑移动到clipboard位置
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;
        Vector3 targetPos = clipboardViewPosition.position;
        Quaternion targetRot = clipboardViewPosition.rotation;

        Debug.Log($"摄像机从 {startPos} 移动到 {targetPos}");

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / transitionDuration;
            float curveValue = transitionCurve.Evaluate(progress);

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, targetRot, curveValue);

            yield return null;
        }

        // 确保最终位置准确
        mainCamera.transform.position = targetPos;
        mainCamera.transform.rotation = targetRot;

        Debug.Log("摄像机切换完成，开始评估");

        // 激活clipboard区域
        if (clipboardReport != null)
        {
            clipboardReport.SetActive(true);
            Debug.Log("激活clipboard区域");
        }

        // 直接启动评估
        scoreManager.SubmitEvaluation();
    }

    public void ReturnToOriginalView()
    {
        if (!isViewingClipboard) return;

        Debug.Log("开始返回原始视角");
        StartCoroutine(TransitionToOriginal());
    }

    private IEnumerator TransitionToOriginal()
    {
        // 隐藏clipboard报告
        if (clipboardReport != null)
        {
            clipboardReport.SetActive(false);
            Debug.Log("隐藏clipboard报告");
        }

        // 重新显示字幕UI（恢复所有子对象）
        if (subtitleUI != null)
        {
            ShowSubtitleCompletely(subtitleUI);
            Debug.Log("重新显示字幕UI及其所有子对象");
        }

        // 摄像机平滑移动回原位置
        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        Debug.Log($"摄像机从 {startPos} 返回到 {originalPosition}");

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / transitionDuration;
            float curveValue = transitionCurve.Evaluate(progress);

            mainCamera.transform.position = Vector3.Lerp(startPos, originalPosition, curveValue);
            mainCamera.transform.rotation = Quaternion.Lerp(startRot, originalRotation, curveValue);

            yield return null;
        }

        // 确保最终位置准确
        mainCamera.transform.position = originalPosition;
        mainCamera.transform.rotation = originalRotation;

        // 显示"Press Enter"提示文本（但要先检查条件）
        UpdateInstructionTextVisibility();

        isViewingClipboard = false;
        Debug.Log("返回原始视角完成");
    }

    // 更新"Press Enter to go to the report."提示文本的显示状态
    private void UpdateInstructionTextVisibility()
    {
        if (instructionText == null) return;

        // 检查ScoreManager的对话状态
        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager == null)
        {
            scoreManager = FindObjectOfType<ScoreManager>();
        }

        bool shouldShow = false;
        if (scoreManager != null)
        {
            // 只有当对话数达到5轮且不在clipboard视角时才显示
            shouldShow = CanViewReport(scoreManager) && !isViewingClipboard;
        }

        // 只在状态改变时更新，避免频繁操作
        bool currentlyActive = instructionText.activeSelf;
        if (currentlyActive != shouldShow)
        {
            instructionText.SetActive(shouldShow);
            if (shouldShow)
            {
                Debug.Log("✅ 条件满足！显示'Press Enter to go to the report.'提示文本");
            }
            else
            {
                Debug.Log("❌ 隐藏'Press Enter to go to the report.'提示文本");
            }
        }
    }

    // 检查当前是否在查看clipboard
    public bool IsViewingClipboard()
    {
        return isViewingClipboard;
    }

    // 强制隐藏字幕UI及其所有子对象
    private void HideSubtitleCompletely(GameObject subtitleObj)
    {
        if (subtitleObj == null) return;

        // 隐藏自身
        subtitleObj.SetActive(false);

        // 递归隐藏所有子对象
        foreach (Transform child in subtitleObj.transform)
        {
            child.gameObject.SetActive(false);
        }

        Debug.Log($"完全隐藏了 {subtitleObj.name} 及其 {subtitleObj.transform.childCount} 个子对象");
    }

    // 恢复显示字幕UI及其所有子对象
    private void ShowSubtitleCompletely(GameObject subtitleObj)
    {
        if (subtitleObj == null) return;

        // 显示自身
        subtitleObj.SetActive(true);

        // 递归显示所有子对象
        foreach (Transform child in subtitleObj.transform)
        {
            child.gameObject.SetActive(true);
        }

        Debug.Log($"完全显示了 {subtitleObj.name} 及其 {subtitleObj.transform.childCount} 个子对象");
    }
}