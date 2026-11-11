using System;
using UnityEngine;

/// <summary>
/// Глобальное состояние текущего забега и игровые события.
/// Сбрасывается при каждой загрузке сцены (GameManager.Init).
/// </summary>
public static class GameState
{
    /// <summary>Открыто меню / экран смерти — игрок не управляет персонажем.</summary>
    public static bool InputBlocked;

    public static bool PlayerDead;
    public static bool Victory;

    // Статистика забега
    public static int RunKills;
    public static int RunSouls;
    public static float RunStartTime;

    /// <summary>Враг убит.</summary>
    public static event Action<EnemyBase> EnemyKilled;

    /// <summary>Попадание по врагу: (враг, урон, совпала ли масть).</summary>
    public static event Action<EnemyBase, float, bool> EnemyHit;

    /// <summary>Короткое сообщение по центру экрана.</summary>
    public static event Action<string, Color> Announce;

    public static void ResetRun()
    {
        InputBlocked = false;
        PlayerDead = false;
        Victory = false;
        RunKills = 0;
        RunSouls = 0;
        RunStartTime = Time.time;

        EnemyKilled = null;
        EnemyHit = null;
        Announce = null;
    }

    public static void RaiseEnemyKilled(EnemyBase enemy)
    {
        RunKills++;
        EnemyKilled?.Invoke(enemy);
    }

    public static void RaiseEnemyHit(EnemyBase enemy, float damage, bool suitMatch)
    {
        EnemyHit?.Invoke(enemy, damage, suitMatch);
    }

    public static void Say(string text, Color color)
    {
        Announce?.Invoke(text, color);
    }

    public static void Say(string text)
    {
        Announce?.Invoke(text, Color.white);
    }
}
