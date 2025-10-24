using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public class WeatherManager : MonoBehaviour
{
    public enum WeatherType { Sunny, Rainy, Windy, Snowy }

    [Header("Runtime / Time")]
    [Tooltip("If true the manager uses real UK time. If false you can simulate weather changes by changing secondsPerDayForTesting.")]
    public bool useRealUKTime = true;

    [Tooltip("Only used when useRealUKTime = false (for debugging). Seconds representing one weather change interval.")]
    public float secondsPerDayForTesting = 5f;

    [Header("Week / Run settings")]
    [Tooltip("Maximum number of consecutive days with the same weather (1..3 typical).")]
    [Range(1, 5)]
    public int maxRunLength = 3;

    [Header("UI")]
    public Image weatherIcon;
    public Sprite sunnySprite;
    public Sprite rainySprite;
    public Sprite windySprite;
    public Sprite snowySprite;
    public Text dayLabel;

    [Header("2D URP Lighting")]
    public Light2D sunLight2D;
    public float sunny2DLightIntensity = 1.2f;
    public float rainy2DLightIntensity = 0.6f;
    public float windy2DLightIntensity = 0.9f;
    public float snowy2DLightIntensity = 0.8f;
    public Color sunny2DLightColor = Color.white;
    public Color rainy2DLightColor = new Color(0.7f, 0.7f, 1f);
    public Color windy2DLightColor = new Color(0.9f, 0.9f, 1f);
    public Color snowy2DLightColor = new Color(0.8f, 0.9f, 1f);

    [Header("Visuals")]
    public ParticleSystem rainParticles;
    public ParticleSystem windyParticles;
    public ParticleSystem snowParticles;

    [Header("Snow Settings")]
    [Tooltip("How long (in seconds) crops stay frozen during snowy weather.")]
    public float freezeDuration = 10f;

    [Header("Behaviour / Debug")]
    public bool debugLogs = true;

    DateTime currentUKDate;
    public WeatherType currentWeather;
    Coroutine tickCoroutine;

    const string PREF_PREFIX = "Weather_";
    System.Random rng;
    bool isFreezingCrops = false;

    void Start()
    {
        rng = new System.Random();
        currentUKDate = GetNowInUK().Date;

        EnsureWeatherForDate(currentUKDate);
        ApplyWeatherForDate(currentUKDate);

        if (useRealUKTime)
        {
            if (debugLogs) Debug.Log("[WeatherManager] Using real UK time. Scheduling midnight update.");
            StartCoroutine(WaitUntilNextUKMidnightAndTick());
        }
        else
        {
            if (debugLogs) Debug.Log("[WeatherManager] Using test time. Seconds per weather = " + secondsPerDayForTesting);
            tickCoroutine = StartCoroutine(TestWeatherTicker(secondsPerDayForTesting));
        }
    }

    #region Time helpers (UK)
    DateTime GetNowInUK()
    {
        DateTime utcNow = DateTime.UtcNow;
        try
        {
            TimeZoneInfo ukZone = null;
            try { ukZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London"); }
            catch { ukZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"); }
            if (ukZone != null)
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, ukZone);
        }
        catch (Exception e)
        {
            if (debugLogs) Debug.LogWarning("[WeatherManager] TimeZone conversion failed: " + e.Message);
        }

        if (debugLogs) Debug.LogWarning("[WeatherManager] Falling back to local time.");
        return DateTime.Now;
    }

    double SecondsUntilNextUKMidnight()
    {
        DateTime nowUK = GetNowInUK();
        DateTime tomorrowMidnightUK = nowUK.Date.AddDays(1);
        return (tomorrowMidnightUK - nowUK).TotalSeconds;
    }
    #endregion

    #region Persistence & generation
    string KeyForDate(DateTime date)
    {
        return PREF_PREFIX + date.ToString("yyyy-MM-dd");
    }

    void EnsureWeatherForDate(DateTime date)
    {
        string key = KeyForDate(date);
        if (PlayerPrefs.HasKey(key)) return;

        DateTime prevDate = date.AddDays(-1);
        string prevKey = KeyForDate(prevDate);

        WeatherType prevWeather = WeatherType.Sunny;
        if (PlayerPrefs.HasKey(prevKey))
        {
            Enum.TryParse(PlayerPrefs.GetString(prevKey), out prevWeather);
        }

        WeatherType newWeather;
        do
        {
            newWeather = (WeatherType)rng.Next(0, 4);
        } while (newWeather == prevWeather);

        int run = rng.Next(1, maxRunLength + 1);

        for (int i = 0; i < run; i++)
        {
            DateTime d = date.AddDays(i);
            string k = KeyForDate(d);
            if (!PlayerPrefs.HasKey(k))
            {
                PlayerPrefs.SetString(k, newWeather.ToString());
                if (debugLogs) Debug.Log($"[WeatherManager] Assigned {newWeather} to {d:yyyy-MM-dd}");
            }
        }

        PlayerPrefs.Save();
    }

    WeatherType WeatherForDate(DateTime date)
    {
        string key = KeyForDate(date);
        if (!PlayerPrefs.HasKey(key))
        {
            EnsureWeatherForDate(date);
        }

        string stored = PlayerPrefs.GetString(key, WeatherType.Sunny.ToString());
        if (Enum.TryParse(stored, out WeatherType wt)) return wt;

        return WeatherType.Sunny;
    }
    #endregion

    #region Apply + Visuals
    void ApplyWeatherForDate(DateTime date)
    {
        currentUKDate = date.Date;
        if (useRealUKTime)
        {
            currentWeather = WeatherForDate(date);
        }

        if (dayLabel != null)
        {
            dayLabel.text = currentUKDate.ToString("ddd") + " - " + currentWeather.ToString();
        }

        if (weatherIcon != null)
        {
            switch (currentWeather)
            {
                case WeatherType.Sunny: weatherIcon.sprite = sunnySprite; break;
                case WeatherType.Rainy: weatherIcon.sprite = rainySprite; break;
                case WeatherType.Windy: weatherIcon.sprite = windySprite; break;
                case WeatherType.Snowy: weatherIcon.sprite = snowySprite; break;
            }
            weatherIcon.enabled = true;
        }

        if (rainParticles != null && rainParticles.isPlaying)
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (windyParticles != null && windyParticles.isPlaying)
            windyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (snowParticles != null && snowParticles.isPlaying)
            snowParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (sunLight2D != null)
        {
            switch (currentWeather)
            {
                case WeatherType.Sunny:
                    sunLight2D.intensity = sunny2DLightIntensity;
                    sunLight2D.color = sunny2DLightColor;
                    break;
                case WeatherType.Rainy:
                    sunLight2D.intensity = rainy2DLightIntensity;
                    sunLight2D.color = rainy2DLightColor;
                    if (rainParticles != null) rainParticles.Play();
                    break;
                case WeatherType.Windy:
                    sunLight2D.intensity = windy2DLightIntensity;
                    sunLight2D.color = windy2DLightColor;
                    if (windyParticles != null) windyParticles.Play();
                    break;
                case WeatherType.Snowy:
                    sunLight2D.intensity = snowy2DLightIntensity;
                    sunLight2D.color = snowy2DLightColor;
                    if (snowParticles != null) snowParticles.Play();
                    break;
            }
        }

        ApplyEffectsToPlots(currentWeather);

        if (debugLogs) Debug.Log($"[WeatherManager] Applied {currentWeather} to {currentUKDate:yyyy-MM-dd}");
    }

    void ApplyEffectsToPlots(WeatherType weather)
    {
        float baseSpeed = 1f;
        AcreManager[] plots = FindObjectsOfType<AcreManager>();

        foreach (var p in plots)
        {
            if (p == null) continue;

            switch (weather)
            {
                case WeatherType.Rainy:
                    p.isDry = false;
                    p.speed = baseSpeed * 1.5f;
                    break;

                case WeatherType.Sunny:
                    if (!p.isDry)
                        p.speed = baseSpeed * 1.2f;
                    else
                        p.speed = baseSpeed * 0.6f;
                    break;

                case WeatherType.Windy:
                    p.isDry = UnityEngine.Random.value < 0.3f;
                    p.speed = baseSpeed * 0.8f;
                    break;

                case WeatherType.Snowy:
                    p.isDry = true;
                    p.speed = 0f;
                    if (!isFreezingCrops)
                        StartCoroutine(FreezeCropsTemporarily(plots));
                    break;
            }
        }

        if (debugLogs) Debug.Log($"[WeatherManager] Effects applied to {plots.Length} plots for {weather}");
    }

    IEnumerator FreezeCropsTemporarily(AcreManager[] plots)
    {
        isFreezingCrops = true;

        if (debugLogs) Debug.Log($"[WeatherManager] Crops frozen for {freezeDuration} seconds!");

        yield return new WaitForSeconds(freezeDuration);

        foreach (var p in plots)
        {
            if (p == null) continue;
            p.speed = 1f;
        }

        isFreezingCrops = false;
        if (debugLogs) Debug.Log("[WeatherManager] Crops unfrozen.");
    }
    #endregion

    #region Ticking
    IEnumerator WaitUntilNextUKMidnightAndTick()
    {
        while (true)
        {
            double secs = SecondsUntilNextUKMidnight();
            if (debugLogs) Debug.Log($"[WeatherManager] Seconds until next UK midnight: {secs:F0}");

            if (secs < 1) secs = 1;
            yield return new WaitForSeconds((float)secs + 0.5f);

            DateTime newUKDate = GetNowInUK().Date;
            EnsureWeatherForDate(newUKDate);
            ApplyWeatherForDate(newUKDate);
        }
    }

    IEnumerator TestWeatherTicker(float secondsPerWeather)
    {
        while (true)
        {
            yield return new WaitForSeconds(secondsPerWeather);
            currentWeather = (WeatherType)rng.Next(0, 4);
            ApplyWeatherForDate(currentUKDate);
        }
    }
    #endregion

    #region Utilities
    public void ForceAdvanceForTesting()
    {
        DateTime next = currentUKDate.AddDays(1);
        EnsureWeatherForDate(next);
        ApplyWeatherForDate(next);
    }

    public void ClearStoredWeather()
    {
        var keysToRemove = new List<string>();
        foreach (var kv in PlayerPrefsKeys())
        {
            if (kv.StartsWith(PREF_PREFIX)) keysToRemove.Add(kv);
        }
        foreach (var k in keysToRemove) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        if (debugLogs) Debug.Log("[WeatherManager] Cleared stored weather keys.");
    }

    IEnumerable<string> PlayerPrefsKeys()
    {
        DateTime start = GetNowInUK().Date.AddDays(-60);
        for (int i = 0; i < 120; i++)
            yield return KeyForDate(start.AddDays(i));
    }
    #endregion

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        ApplyWeatherForDate(currentUKDate);
    }
#endif
}
