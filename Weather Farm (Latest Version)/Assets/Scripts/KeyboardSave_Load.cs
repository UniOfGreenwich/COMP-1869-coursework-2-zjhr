using UnityEngine;

public class KeyboardSaveLoad : MonoBehaviour
{
    [Header("Key Bindings")]
    public KeyCode saveKey = KeyCode.F5;
    public KeyCode loadKey = KeyCode.F9;

    private FarmManager farmManager;
    private AcreManager[] acres;
    private WeatherManager weatherManager;

    void Start()
    {
        farmManager = FindObjectOfType<FarmManager>();
        acres = FindObjectsOfType<AcreManager>();
        weatherManager = FindObjectOfType<WeatherManager>();
    }

    void Update()
    {
        if (Input.GetKeyDown(saveKey))
            SaveGame();

        if (Input.GetKeyDown(loadKey))
            LoadGame();
    }

    void SaveGame()
    {
        if (farmManager != null)
            PlayerPrefs.SetInt("PlayerMoney", farmManager.money);

        foreach (var acre in acres)
        {
            string plotID = acre.gameObject.name;
            PlayerPrefs.SetInt(plotID + "_Stage", acre.plantStage);
            PlayerPrefs.SetFloat(plotID + "_Timer", acre.timer);
            PlayerPrefs.SetInt(plotID + "_IsPlanted", acre.isPlanted ? 1 : 0);
            PlayerPrefs.SetInt(plotID + "_IsDry", acre.isDry ? 1 : 0);
            PlayerPrefs.SetInt(plotID + "_IsBought", acre.isBought ? 1 : 0);
            string plantName = acre.selectedPlant != null ? acre.selectedPlant.plantName : "";
            PlayerPrefs.SetString(plotID + "_PlantName", plantName);
        }

        if (weatherManager != null)
            PlayerPrefs.SetString("CurrentWeather", weatherManager.currentWeather.ToString());

        PlayerPrefs.Save();
        Debug.Log("Game Saved!");
    }

    void LoadGame()
    {
        if (farmManager != null)
        {
            farmManager.money = PlayerPrefs.GetInt("PlayerMoney", 100);
            farmManager.moneyTxt.text = "$" + farmManager.money;
        }

        foreach (var acre in acres)
        {
            string plotID = acre.gameObject.name;
            acre.plantStage = PlayerPrefs.GetInt(plotID + "_Stage", 0);
            acre.timer = PlayerPrefs.GetFloat(plotID + "_Timer", 0f);
            acre.isPlanted = PlayerPrefs.GetInt(plotID + "_IsPlanted", 0) == 1;
            acre.isDry = PlayerPrefs.GetInt(plotID + "_IsDry", 1) == 1;
            acre.isBought = PlayerPrefs.GetInt(plotID + "_IsBought", 1) == 1;

            string plantName = PlayerPrefs.GetString(plotID + "_PlantName", "");
            if (!string.IsNullOrEmpty(plantName))
            {
                var store = FindObjectOfType<StoreManager>();
                foreach (var plantItem in store.GetComponentsInChildren<PlantItem>())
                {
                    if (plantItem.plant.plantName == plantName)
                    {
                        acre.selectedPlant = plantItem.plant;
                        break;
                    }
                }
            }

            if (acre.plant != null)
            {
                acre.plant.gameObject.SetActive(acre.isPlanted);
                if (acre.isPlanted && acre.selectedPlant != null && !acre.isDry)
                    acre.plant.sprite = acre.selectedPlant.plantStages[acre.plantStage];
                else if (acre.isDry && acre.selectedPlant != null)
                    acre.plant.sprite = acre.selectedPlant.dryPlanted;
            }

            if (acre.plot != null)
                acre.plot.sprite = acre.isBought ? acre.normalSprite : acre.unavailableSprite;
        }

        if (weatherManager != null)
        {
            string savedWeather = PlayerPrefs.GetString("CurrentWeather", "Sunny");
            if (System.Enum.TryParse(savedWeather, out WeatherManager.WeatherType wt))
            {
                weatherManager.currentWeather = wt;

                if (weatherManager.weatherIcon != null)
                {
                    switch (wt)
                    {
                        case WeatherManager.WeatherType.Sunny:
                            weatherManager.weatherIcon.sprite = weatherManager.sunnySprite;
                            break;
                        case WeatherManager.WeatherType.Rainy:
                            weatherManager.weatherIcon.sprite = weatherManager.rainySprite;
                            break;
                        case WeatherManager.WeatherType.Windy:
                            weatherManager.weatherIcon.sprite = weatherManager.windySprite;
                            break;
                        case WeatherManager.WeatherType.Snowy:
                            weatherManager.weatherIcon.sprite = weatherManager.snowySprite;
                            break;
                    }
                }

                if (weatherManager.sunLight2D != null)
                {
                    switch (wt)
                    {
                        case WeatherManager.WeatherType.Sunny:
                            weatherManager.sunLight2D.intensity = weatherManager.sunny2DLightIntensity;
                            weatherManager.sunLight2D.color = weatherManager.sunny2DLightColor;
                            break;
                        case WeatherManager.WeatherType.Rainy:
                            weatherManager.sunLight2D.intensity = weatherManager.rainy2DLightIntensity;
                            weatherManager.sunLight2D.color = weatherManager.rainy2DLightColor;
                            break;
                        case WeatherManager.WeatherType.Windy:
                            weatherManager.sunLight2D.intensity = weatherManager.windy2DLightIntensity;
                            weatherManager.sunLight2D.color = weatherManager.windy2DLightColor;
                            break;
                        case WeatherManager.WeatherType.Snowy:
                            weatherManager.sunLight2D.intensity = weatherManager.snowy2DLightIntensity;
                            weatherManager.sunLight2D.color = weatherManager.snowy2DLightColor;
                            break;
                    }
                }

                if (weatherManager.rainParticles != null)
                {
                    if (wt == WeatherManager.WeatherType.Rainy)
                    {
                        if (!weatherManager.rainParticles.isPlaying)
                            weatherManager.rainParticles.Play();
                    }
                    else
                    {
                        if (weatherManager.rainParticles.isPlaying)
                            weatherManager.rainParticles.Stop();
                    }
                }

                if (weatherManager.dayLabel != null)
                    weatherManager.dayLabel.text = System.DateTime.Now.ToString("ddd") + " - " + wt.ToString();
            }
        }

        Debug.Log("Game Loaded!");
    }
}
