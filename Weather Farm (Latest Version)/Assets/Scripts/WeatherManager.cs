using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WeatherManager : MonoBehaviour
{
    public enum WeatherType { Sunny, Rainy, Windy }

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
    public Text dayLabel;

    [Header("Visuals")]
    public Light sunLight;
    public float sunLightNormalIntensity = 0.6f;
    public float sunLightSunnyIntensity = 1.2f;
    public float sunLightWindyIntensity = 0.9f;
    public ParticleSystem rainParticles;
    public ParticleSystem windyParticles;

    [Header("Behaviour / Debug")]
    public bool debugLogs = true;

    // Internal
    DateTime currentUKDate;
    public WeatherType currentWeather;
    Coroutine tickCoroutine;

    const string PREF_PREFIX = "Weather_";
    System.Random rng;

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
            catch { }
            if (ukZone == null)
            {
                try { ukZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"); }
                catch { }
            }
            if (ukZone != null)
            {
                DateTime ukNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, ukZone);
                return ukNow;
            }
        }
        catch (Exception e)
        {
            if (debugLogs) Debug.LogWarning("[WeatherManager] TimeZone conversion failed: " + e.Message);
        }

        if (debugLogs) Debug.LogWarning("[WeatherManager] Falling back to local time for date. Ensure device timezone is UK for accurate behaviour.");
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
            newWeather = (WeatherType)rng.Next(0, 3);
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

        // Current weather can be changed manually
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
            }
            weatherIcon.enabled = true;
        }

        if (sunLight != null)
        {
            if (currentWeather == WeatherType.Sunny) sunLight.intensity = sunLightSunnyIntensity;
            else if (currentWeather == WeatherType.Rainy) sunLight.intensity = sunLightNormalIntensity;
            else if (currentWeather == WeatherType.Windy) sunLight.intensity = sunLightWindyIntensity;
        }

        if (rainParticles != null && rainParticles.isPlaying)
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (windyParticles != null && windyParticles.isPlaying)
            windyParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (currentWeather == WeatherType.Rainy && rainParticles != null)
            rainParticles.Play();
        else if (currentWeather == WeatherType.Windy && windyParticles != null)
            windyParticles.Play();

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

            if (weather == WeatherType.Rainy)
            {
                p.isDry = false;
                p.speed = baseSpeed * 1.5f;
            }
            else if (weather == WeatherType.Sunny)
            {
                bool currentlyWatered = !p.isDry;
                if (currentlyWatered)
                {
                    p.speed = baseSpeed * 1.2f;
                    p.isDry = false;
                }
                else
                {
                    p.isDry = true;
                    p.speed = baseSpeed * 0.6f;
                }
            }
            else if (weather == WeatherType.Windy)
            {
                p.isDry = UnityEngine.Random.value < 0.3f;
                p.speed = baseSpeed * 0.8f;
            }
        }

        if (debugLogs) Debug.Log($"[WeatherManager] Effects applied to {plots.Length} plots for {weather}");
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
            currentWeather = (WeatherType)rng.Next(0, 3);
            ApplyWeatherForDate(currentUKDate);
        }
    }
    #endregion

    #region Utilities (manual controls)
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
        {
            yield return KeyForDate(start.AddDays(i));
        }
    }
    #endregion

#if UNITY_EDITOR
    // Allows to chnage weather in inspector
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        ApplyWeatherForDate(currentUKDate);
    }
#endif
}
