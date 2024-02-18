using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using PimDeWitte.UnityMainThreadDispatcher;
[Serializable]
public enum RewardType
{
    Cooktime,
    Profit,
    Speed,
    Offline,
    CoinBonus,
    JellyBonus
}

public class AdMobManager : MonoBehaviour
{
#if UNITY_ANDROID
    private string _adUnitId = "ca-app-pub-3940256099942544/5224354917";
#elif UNITY_IPHONE
  private string _adUnitId = "ca-app-pub-3940256099942544/1712485313";
#else
  private string _adUnitId = "unused";
#endif

    public AdUI adUI;
    public OfflineRewardUI offlineRewardUI;
    public ShopUI shopUI;
    private const int MaxAdsPerDay = 3; // 각 보상 유형별 최대 광고 표시 횟수
    private RewardedAd rewardedAd;
    private RewardType currentRewardType= RewardType.Cooktime;//default

    private float rewardMaintainTime=60f;
    private static AdMobManager instance;
    private AdData adData;

    private string lastAdsDate="";
    private string lastExitTime;
    private string EnterTime;
    private int tmpEarning;
    public delegate void RewardValidateDelegate(float rewardTime);
    public event RewardValidateDelegate OnRewardValidateDelegate;
    private Transform coinReward;
    private Transform jellyReward;
    public static AdMobManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<AdMobManager>();
            }
            return instance;
        }
    }
    private Dictionary<RewardType, int> adsCountPerRewardType = new Dictionary<RewardType, int>();
    void Start()
    {
        MobileAds.Initialize((InitializationStatus initStatus) =>
        {
            // This callback is called once the MobileAds SDK is initialized.
            Debug.Log("Ad Loaded Correctly");
        });
        coinReward=shopUI.coinRewardButton.transform;
        jellyReward = shopUI.jellyRewardButton.transform;
    }

    public void LoadRewardedAdForCooktime()
    {
        StartCoroutine(ShowLoadingPanelThenLoadAd(RewardType.Cooktime));
    }

    public void LoadRewardedAdForProfit()
    {
        StartCoroutine(ShowLoadingPanelThenLoadAd(RewardType.Profit));
    }

    public void LoadRewardedAdForSpeed()
    {
        StartCoroutine(ShowLoadingPanelThenLoadAd(RewardType.Speed));
    }

    IEnumerator ShowLoadingPanelThenLoadAd(RewardType rewardType)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => adUI.adLoadingPanel.SetActive(true));
        UnityMainThreadDispatcher.Instance().Enqueue(() => offlineRewardUI.adLoadingPanel.SetActive(true));
        UnityMainThreadDispatcher.Instance().Enqueue(() => shopUI.adLoadingPanel.SetActive(true));
        Debug.Log("Loading Panel Activated");

        yield return new WaitForSeconds(0.5f); // 필요한 경우 로딩 패널을 보여주기 위한 짧은 대기 시간

        LoadRewardedAd(rewardType, () =>
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => adUI.adLoadingPanel.SetActive(false));
            UnityMainThreadDispatcher.Instance().Enqueue(() => offlineRewardUI.adLoadingPanel.SetActive(false));
            UnityMainThreadDispatcher.Instance().Enqueue(() => shopUI.adLoadingPanel.SetActive(false));
            Debug.Log("Ad Loaded, Loading Panel Deactivated");
        });
    }

    public void LoadRewardedAdForOffline(int earnings)
    {
        tmpEarning=earnings*3;
        StartCoroutine(ShowLoadingPanelThenLoadAd(RewardType.Offline));
    }
    public void LoadRewardedAdForCoinBonus(int earnings)
    {
        tmpEarning= earnings;
        StartCoroutine(ShowLoadingPanelThenLoadAd(RewardType.CoinBonus));
    }
    public void LoadRewardedAdForJellyBonus(int earnings)
    {
        tmpEarning = earnings;
        StartCoroutine(ShowLoadingPanelThenLoadAd(RewardType.JellyBonus));
    }
    public void LoadRewardedAd(RewardType rewardType, Action onAdLoaded)
    {
        // Clean up the old ad before loading a new one.
        if (rewardedAd != null)
        {
            rewardedAd.Destroy();
            rewardedAd = null;
        }
        currentRewardType= rewardType;
        Debug.Log("Loading the rewarded ad.");

        // create our request used to load the ad.
        var adRequest = new AdRequest();

        // send the request to load the ad.
        RewardedAd.Load(_adUnitId, adRequest,
            (RewardedAd ad, LoadAdError error) =>
            {
                // if error is not null, the load request failed.
                if (error != null || ad == null)
                {
                    Debug.LogError("Rewarded ad failed to load an ad " +
                                   "with error : " + error);
                    UnityMainThreadDispatcher.Instance().Enqueue(()=>adUI.adLoadingPanel.SetActive(false));
                    UnityMainThreadDispatcher.Instance().Enqueue(()=>offlineRewardUI.adLoadingPanel.SetActive(false));
                    UnityMainThreadDispatcher.Instance().Enqueue(()=>shopUI.adLoadingPanel.SetActive(false));
                    return;
                }
                onAdLoaded?.Invoke();
                Debug.Log("Rewarded ad loaded with response : "
                          + ad.GetResponseInfo());

                rewardedAd = ad;

                RegisterEventHandlers(rewardedAd);

                ShowRewardedAd();
            });
    }
    public void ShowRewardedAd()
    {
        Debug.Log("Show Reward Try");
        //ui 버튼 처리에 대한 부분은 UI Manager에서
        if (rewardedAd != null && rewardedAd.CanShowAd())
        {
            Debug.Log("Show Reward Success");
            rewardedAd.Show((Reward reward) =>
            {
                switch (currentRewardType)
                {
                    case RewardType.Cooktime:
                    //2차적으로 이미 활성화 되어있을때는 제공하지 않음
                        if(OrderManager.Instance!=null&& !OrderManager.Instance.isRewardActivated)
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(OrderManager.Instance.SetIsRewardActivated(rewardMaintainTime));
                            UnityMainThreadDispatcher.Instance().Enqueue(DecreaseAdCount(currentRewardType));
                            //OnRewardValidateDelegate?.Invoke(rewardMaintainTime);
                        }
                        break;
                    case RewardType.Profit:
                        if (CustomerManager.Instance != null && !CustomerManager.Instance.isRewardActivated)
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(CustomerManager.Instance.SetIsRewardActivated(rewardMaintainTime));
                            UnityMainThreadDispatcher.Instance().Enqueue(DecreaseAdCount(currentRewardType));
                        }
                        break;
                    case RewardType.Speed:
                        if (DataManager.Instance != null && !DataManager.Instance.isRewardActivated)
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(DataManager.Instance.SetIsRewardActivated(rewardMaintainTime));
                            UnityMainThreadDispatcher.Instance().Enqueue(DecreaseAdCount(currentRewardType));
                        }
                        break;
                    case RewardType.Offline:
                        if (BusinessGameManager.Instance!=null) UnityMainThreadDispatcher.Instance().Enqueue(()=>BusinessGameManager.Instance.AddMoney(tmpEarning));
                        //사실상 오프라인 보상에 대해서는 Count 적용 X
                        break;
                    case RewardType.CoinBonus:
                        if (BusinessGameManager.Instance != null) UnityMainThreadDispatcher.Instance()
                        .Enqueue(UIManager.Instance.PlayCoinAttraction(coinReward, tmpEarning));
                        UnityMainThreadDispatcher.Instance().Enqueue(DecreaseAdCount(currentRewardType));
                        UnityMainThreadDispatcher.Instance().Enqueue(()=>shopUI.InitADUI());
                        break;
                    case RewardType.JellyBonus:
                        if (DataManager.Instance != null) UnityMainThreadDispatcher.Instance()
                        .Enqueue(UIManager.Instance.PlayJellyAttraction(jellyReward,tmpEarning));
                        UnityMainThreadDispatcher.Instance().Enqueue(DecreaseAdCount(currentRewardType));
                        UnityMainThreadDispatcher.Instance().Enqueue(() => shopUI.InitADUI());
                        break;
                    default:
                        Debug.LogError("Unknown reward type.");
                        break;
                }
            });
        }
    }

    private void RegisterEventHandlers(RewardedAd ad)
    {
        // Raised when the ad is estimated to have earned money.
        ad.OnAdPaid += (AdValue adValue) =>
        {
            Debug.Log(String.Format("Rewarded ad paid {0} {1}.",
                adValue.Value,
                adValue.CurrencyCode));
        };
        // Raised when an impression is recorded for an ad.
        ad.OnAdImpressionRecorded += () =>
        {
            Debug.Log("Rewarded ad recorded an impression.");
        };
        // Raised when a click is recorded for an ad.
        ad.OnAdClicked += () =>
        {
            Debug.Log("Rewarded ad was clicked.");
        };
        // Raised when an ad opened full screen content.
        ad.OnAdFullScreenContentOpened += () =>
        {
            Debug.Log("Rewarded ad full screen content opened.");
        };
        // Raised when the ad closed full screen content.
        ad.OnAdFullScreenContentClosed += () =>
        {
            Debug.Log("Rewarded ad full screen content closed.");
            //LoadRewardedAd();
        };
        // Raised when the ad failed to open full screen content.
        ad.OnAdFullScreenContentFailed += (AdError error) =>
        {
            Debug.LogError("Rewarded ad failed to open full screen content " +
                           "with error : " + error);
            //LoadRewardedAd();
        };
    }
    public bool CanShowAd(RewardType rewardType)
    {
        Debug.Log(adsCountPerRewardType.ContainsKey(rewardType)+" "+adsCountPerRewardType[rewardType]);
        return adsCountPerRewardType.ContainsKey(rewardType) && adsCountPerRewardType[rewardType] > 0;
    }

    public IEnumerator DecreaseAdCount(RewardType rewardType)
    {
        if (CanShowAd(rewardType))
        {
            adsCountPerRewardType[rewardType]--;
            Debug.Log($"[AdMobManager] Current ads count for {rewardType}: {adsCountPerRewardType[rewardType]}");
            yield break;
        }
    }
    public AdData GetData(){
        string today = DateTime.Now.ToString("yyyyMMdd");
        adData = AdData.FromDictionary(adsCountPerRewardType, today);
        return adData;
    }
    public void SetData(SystemData data)
    {
        adData=data.adData;
        InitializeAdsCount();
        CheckDateAndUpdateAdsCount();
        adUI.InitUI();
        shopUI.InitADUI();
    }
    public void InitializeAdsCount()
    {
        Dictionary<RewardType, int> currentCounts = adData.ToDictionary();

        foreach (RewardType rewardType in Enum.GetValues(typeof(RewardType)))
        {
            if (!currentCounts.ContainsKey(rewardType))
            {
                currentCounts[rewardType] = MaxAdsPerDay;
            }
        }
        adData = AdData.FromDictionary(currentCounts, adData.lastAdsDate);
        adsCountPerRewardType = currentCounts;
    }

    void CheckDateAndUpdateAdsCount()
    {
        string today = DateTime.Now.ToString("yyyyMMdd");

        if (adData.lastAdsDate != today)
        {
            Dictionary<RewardType, int> newCounts = Enum.GetValues(typeof(RewardType))
                .Cast<RewardType>()
                .ToDictionary(rewardType => rewardType, rewardType => MaxAdsPerDay);
            adData = AdData.FromDictionary(newCounts, today);
        }
    }
    public int GetRemainingAdsCount(RewardType rewardType)
    {
        //AdUI에서 남은 광고 횟수 호출
        if (adsCountPerRewardType.TryGetValue(rewardType, out int count))
        {
            Debug.Log("ads left : "+count);
            return count;
        }
        return 0; 
    }
    public int GetMaxAdsCount()
    {
        return MaxAdsPerDay;
    }

}
