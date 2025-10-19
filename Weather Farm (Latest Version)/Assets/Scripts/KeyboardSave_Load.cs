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

        //Loads each acre
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

            // Updates the plant visuals
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

        // Load the weather when the save was made
        if (weatherManager != null)
        {
            string savedWeather = PlayerPrefs.GetString("CurrentWeather", "Sunny");
            if (System.Enum.TryParse(savedWeather, out WeatherManager.WeatherType wt))
            {
                weatherManager.currentWeather = wt;
                if (weatherManager.weatherIcon != null)
                    weatherManager.weatherIcon.sprite = wt == WeatherManager.WeatherType.Sunny
                        ? weatherManager.sunnySprite
                        : weatherManager.rainySprite;

                if (weatherManager.sunLight != null)
                    weatherManager.sunLight.intensity = wt == WeatherManager.WeatherType.Sunny
                        ? weatherManager.sunLightSunnyIntensity
                        : weatherManager.sunLightNormalIntensity;

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