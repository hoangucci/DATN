using MidnightChaos.Combat;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MidnightChaos.UI
{
    public class PlayerHealthBarUI : MonoBehaviour
    {
        [Header("UI Component References")]
        [SerializeField] private GameObject healthPanel;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Image damageGhostFillImage;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text playerNameText;

        [Header("Visual Customization")]
        [SerializeField] private Color fullHealthColor = new Color(0.2f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color midHealthColor = new Color(0.95f, 0.75f, 0.15f, 1f);
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField] private float smoothSpeed = 8f;

        private NetworkHealth localPlayerHealth;
        private float targetFill = 1f;

        private void Start()
        {
            if (healthFillImage != null)
            {
                healthFillImage.type = Image.Type.Filled;
                healthFillImage.fillMethod = Image.FillMethod.Horizontal;
            }

            if (damageGhostFillImage != null)
            {
                damageGhostFillImage.type = Image.Type.Filled;
                damageGhostFillImage.fillMethod = Image.FillMethod.Horizontal;
            }
        }

        private void Update()
        {
            if (localPlayerHealth == null)
            {
                TryBindLocalPlayerHealth();
                if (localPlayerHealth == null)
                {
                    if (healthPanel != null && healthPanel.activeSelf)
                    {
                        healthPanel.SetActive(false);
                    }
                    return;
                }
            }

            if (healthPanel != null && !healthPanel.activeSelf)
            {
                healthPanel.SetActive(true);
            }

            UpdateHealthUI();
        }

        private void TryBindLocalPlayerHealth()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                return;
            }

            if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                localPlayerHealth = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<NetworkHealth>();
                if (localPlayerHealth != null)
                {
                    localPlayerHealth.HealthChanged += OnHealthChanged;
                    RefreshInstant();
                }
            }
        }

        private void OnDestroy()
        {
            if (localPlayerHealth != null)
            {
                localPlayerHealth.HealthChanged -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(int previousHp, int newHp)
        {
            UpdateTargetFill();
        }

        private void RefreshInstant()
        {
            UpdateTargetFill();
            if (healthFillImage != null) healthFillImage.fillAmount = targetFill;
            if (damageGhostFillImage != null) damageGhostFillImage.fillAmount = targetFill;
        }

        private void UpdateTargetFill()
        {
            if (localPlayerHealth == null) return;

            int maxHp = Mathf.Max(1, localPlayerHealth.MaxHealth);
            int curHp = Mathf.Clamp(localPlayerHealth.CurrentHealth, 0, maxHp);

            targetFill = (float)curHp / maxHp;

            if (healthText != null)
            {
                healthText.text = $"{curHp} / {maxHp}";
            }

            if (playerNameText != null)
            {
                playerNameText.text = localPlayerHealth.DisplayName;
            }
        }

        private void UpdateHealthUI()
        {
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = Mathf.Lerp(healthFillImage.fillAmount, targetFill, Time.deltaTime * smoothSpeed);

                // Update Gradient Color
                if (targetFill > 0.5f)
                {
                    healthFillImage.color = Color.Lerp(midHealthColor, fullHealthColor, (targetFill - 0.5f) * 2f);
                }
                else
                {
                    healthFillImage.color = Color.Lerp(lowHealthColor, midHealthColor, targetFill * 2f);
                }
            }

            if (damageGhostFillImage != null)
            {
                damageGhostFillImage.fillAmount = Mathf.Lerp(damageGhostFillImage.fillAmount, healthFillImage.fillAmount, Time.deltaTime * (smoothSpeed * 0.4f));
            }
        }
    }
}
