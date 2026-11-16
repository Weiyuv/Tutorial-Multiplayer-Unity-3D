
using TMPro;
using UnityEngine;

public class RoundHUD : MonoBehaviour
{
    [Header("Referência")]
    [SerializeField] private RoundManager roundManager;

    [Header("Textos")]
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text enemiesText;
    [SerializeField] private TMP_Text statusText;

    private void Update()
    {
        if (roundManager == null)
            return;

        int round = roundManager.CurrentRound.Value;
        int enemies = roundManager.EnemiesRemaining.Value;

        if (roundText != null)
        {
            roundText.text = round > 0
                ? "ROUND " + round + " / 10"
                : "PREPARANDO...";
        }

        if (enemiesText != null)
            enemiesText.text = "INIMIGOS: " + enemies;

        if (statusText != null)
        {
            if (roundManager.MatchFinished.Value)
            {
                statusText.text = round >= 10
                    ? "VITÓRIA!"
                    : "DERROTA!";
            }
            else if (roundManager.IsIntermission.Value)
            {
                statusText.text =
                    "PRÓXIMO ROUND EM: " +
                    Mathf.CeilToInt(
                        roundManager.IntermissionRemaining.Value
                    );
            }
            else
            {
                statusText.text = "";
            }
        }
    }
}
