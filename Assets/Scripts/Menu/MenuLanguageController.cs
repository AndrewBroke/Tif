using UnityEngine;
using UnityEngine.Localization.Settings;

public class MenuLanguageController : MonoBehaviour
{

    public void SetLanguage(string lang)
    {
        var locale = LocalizationSettings.AvailableLocales
            .Locales
            .Find(x => x.Identifier.Code == lang);

        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
            print("Текущий язык: " + LocalizationSettings.SelectedLocale.Identifier.Code);
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
