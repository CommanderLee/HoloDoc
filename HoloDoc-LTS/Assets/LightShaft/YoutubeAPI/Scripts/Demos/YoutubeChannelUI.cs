using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class YoutubeChannelUI : MonoBehaviour {

    public Text videoName;
    public string videoId, thumbUrl;
    public Image videoThumb;

    public void LoadChannel()
    {
        GameObject.FindObjectOfType<ChannelSearchDemo>().LoadChannelResult(videoId);
    }

    public void LoadThumbnail()
    {
        StartCoroutine(DownloadThumb());
    }

    IEnumerator DownloadThumb()
    {
        WWW www = new WWW(thumbUrl);
        yield return www;
        Texture2D thumb = new Texture2D(100, 100);
        www.LoadImageIntoTexture(thumb);
        videoThumb.sprite = Sprite.Create(thumb, new Rect(0, 0, thumb.width, thumb.height), new Vector2(0.5f, 0.5f), 100);
    }

}
