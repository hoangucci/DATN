using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MidnightChaos.UI
{
    public class IntroStoryManager : MonoBehaviour
    {
        public static IntroStoryManager Instance { get; private set; }

        [Header("UI Canvas Components")]
        [SerializeField] private GameObject storyPanel;
        [SerializeField] private CanvasGroup storyCanvasGroup;
        [SerializeField] private TMP_Text storyText;
        [SerializeField] private TMP_Text continueHintText;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button panelClickButton;
        [SerializeField] private Image backgroundBlackOverlay;

        [Header("Story Lines Configuration")]
        [TextArea(3, 5)]
        [SerializeField] private List<string> storyLines = new List<string>()
        {
            "Chuyến bay gặp sự cố bất thường do từ trường mạnh...\n\nMáy bay mất kiểm soát và rơi tự do xuống một hòn đảo bí ẩn bị bao phủ bởi Năng Lượng Hỗn Mang.",

            "Bạn tỉnh dậy trên bờ bãi biển hoang sơ.\n\nTất cả thiết bị điện tử đều hư hỏng. Không khí trên đảo tràn ngập năng lượng biến đổi nguy hiểm.",

            "Năng lượng Hỗn Mang khiến các sinh vật trở nên hung tợn.\n\nKhi quái vật chết, lõi năng lượng của chúng sẽ nhập vào đồng loại gần nhất, buộc cơ thể chúng phải biến đổi tiến hóa lên dạng nguy hiểm hơn.",

            "Bạn không thể sử dụng trực tiếp nguồn năng lượng này.\n\nCách duy nhất để sống sót: Khai thác tài nguyên, săn quái vật dạng cuối để lấy Chaos Shard, và chế tạo một Con Thuyền đặc biệt để vượt biển thoát khỏi đảo!"
        };

        [Header("Typewriter Settings")]
        [SerializeField] private float typewriterSpeed = 0.035f;
        [SerializeField] private bool triggerOnStart = false;
        [SerializeField] private string targetSceneOnComplete = "ProceduralCombatDemo";

        [Header("Events")]
        public UnityEvent onStoryCompleted;

        public bool IsStoryActive => storyPanel != null && storyPanel.activeSelf;

        private int currentLineIndex = 0;
        private bool isTyping = false;
        private string currentFullText = "";
        private Coroutine typingCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void Start()
        {
            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(SkipStory);
            }

            if (panelClickButton != null)
            {
                panelClickButton.onClick.RemoveAllListeners();
                panelClickButton.onClick.AddListener(OnPlayerClickNext);
            }

            if (triggerOnStart && storyPanel != null)
            {
                StartStorySequence();
            }
            else if (storyPanel != null)
            {
                storyPanel.SetActive(false);
            }
        }

        public void StartStorySequence()
        {
            gameObject.SetActive(true);

            if (storyPanel == null || storyLines.Count == 0)
            {
                OnStoryFinished();
                return;
            }

            storyPanel.SetActive(true);
            storyPanel.transform.SetAsLastSibling();
            if (storyCanvasGroup != null) storyCanvasGroup.alpha = 1f;

            currentLineIndex = 0;
            ShowLine(currentLineIndex);
        }

        private void Update()
        {
            if (storyPanel == null || !storyPanel.activeSelf) return;

            bool nextPressed = false;

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
                    UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    nextPressed = true;
                }
            }

            if (nextPressed)
            {
                OnPlayerClickNext();
            }
        }

        public void OnPlayerClickNext()
        {
            if (isTyping)
            {
                if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                if (storyText != null) storyText.text = currentFullText;
                isTyping = false;
                if (continueHintText != null) continueHintText.text = "Bấm chuột hoặc Space để tiếp tục >>";
            }
            else
            {
                currentLineIndex++;
                if (currentLineIndex < storyLines.Count)
                {
                    ShowLine(currentLineIndex);
                }
                else
                {
                    EndStorySequence();
                }
            }
        }

        private void ShowLine(int index)
        {
            currentFullText = storyLines[index];
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypewriterRoutine(currentFullText));
        }

        private IEnumerator TypewriterRoutine(string line)
        {
            isTyping = true;
            if (storyText != null) storyText.text = "";
            if (continueHintText != null) continueHintText.text = "Đang tải dữ liệu cốt truyện...";

            for (int i = 0; i <= line.Length; i++)
            {
                if (storyText != null) storyText.text = line.Substring(0, i);
                yield return new WaitForSecondsRealtime(typewriterSpeed);
            }

            isTyping = false;
            if (continueHintText != null) continueHintText.text = "Bấm chuột hoặc Space để tiếp tục >>";
        }

        public void SkipStory()
        {
            EndStorySequence();
        }

        private void EndStorySequence()
        {
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            StartCoroutine(FadeOutAndComplete());
        }

        private IEnumerator FadeOutAndComplete()
        {
            float duration = 0.6f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                if (storyCanvasGroup != null) storyCanvasGroup.alpha = alpha;
                yield return null;
            }

            if (storyPanel != null) storyPanel.SetActive(false);

            OnStoryFinished();
        }

        private void OnStoryFinished()
        {
            onStoryCompleted?.Invoke();
            Debug.Log("[IntroStoryManager] Đã hoàn thành dẫn dắt cốt truyện!");

            if (string.IsNullOrEmpty(targetSceneOnComplete) || targetSceneOnComplete.Equals("Map", System.StringComparison.OrdinalIgnoreCase))
            {
                targetSceneOnComplete = "ProceduralCombatDemo";
            }

            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(targetSceneOnComplete) && activeScene != targetSceneOnComplete && (activeScene == "Login" || activeScene == "MainMenu"))
            {
                Debug.Log($"[IntroStoryManager] Đang chuyển sang Scene Gameplay: {targetSceneOnComplete}");
                SceneManager.LoadScene(targetSceneOnComplete);
            }
        }
    }
}
