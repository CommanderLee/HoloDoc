using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System.Linq;
using Newtonsoft.Json;
#if !UNITY_EDITOR
using Microsoft.Graphics.Canvas;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Graphics.Imaging;
using Windows.UI.Input.Inking;
using Windows.UI;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Windows.UI.Core;
#endif

public class MicrosoftAPIHelper {
    #region Static Attirbutes for APIs
    // Replace the subscriptionKey string value with your valid subscription key.
    private static readonly string azureKey = "635b5e31388348f783ac45e1ce3068d7";
    // Note: we removed the prefix and move them in to separate functions (e.g., "ocr").
    private static readonly string azureUri = "https://eastus2.api.cognitive.microsoft.com/vision/v1.0/";

    private static readonly string labsKey = "028d9383a5b9465899aea0c6300ea553";
    private static readonly string labsUri = "https://api.labs.cognitive.microsoft.com/academic/v1.0/";
    
    /// <summary>
    /// Minimum size of image, required by Azure vision service. 
    /// </summary>
    private static double AZURE_API_MIN_SIZE = 55.0f;
    
    public enum SERVICE_STATUS { IDLE, PENDING, DONE, ERROR };
    #endregion

    public SERVICE_STATUS ocrStatus = SERVICE_STATUS.IDLE;
    public string ocrResult = "";
    public int ocrAvgX = -1, ocrAvgY = -1;

    public SERVICE_STATUS inkingStatus = SERVICE_STATUS.IDLE;
    public string inkingResult = "";

    public SERVICE_STATUS academicStatus = SERVICE_STATUS.IDLE;
    public string academicResult = "";
    //public List<ARDocument> academicResult = new List<ARDocument>();
    //public string academicErrInfo = "";

    //public List<ARDocument> parseResults = null;

#if !UNITY_EDITOR

    /// <summary>
    /// Do OCR with Azure service
    /// </summary>
    /// <param name="imageName">Image path in CameraRoll</param>
    /// <param name="topLeft"></param>
    /// <param name="botRight"></param>
    public async void MakeOCRRequest(string imageName, Vector2 topLeft, Vector2 botRight)
    {
        // Check the boundary
        double widthRatio = botRight.x - topLeft.x;
        double heightRatio = botRight.y - topLeft.y;
        ocrStatus = SERVICE_STATUS.PENDING;
        Debug.Log(string.Format("OCR on file:{0}, start point {1}, size {2}, {3}", imageName, topLeft.ToString(), widthRatio, heightRatio));

        HttpClient client = new HttpClient();

        // Request headers.
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", azureKey);

        // Request parameters.
        string requestParameters = "language=en&detectOrientation=true";

        // Assemble the URI for the REST API Call.
        string uri = azureUri + "ocr?" + requestParameters;

        HttpResponseMessage response;

        // Request body. Posts a locally stored JPEG image.
        byte[] byteData = new byte[1];
        byte[] croppedBytes = new byte[1];

        /* Crop Function by Diederik Krols.
        * Refer: https://github.com/XamlBrewer/UWP-ImageCropper-/blob/master/XamlBrewer.Uwp.Controls/Helpers/CropBitmap.cs
        * */
        StorageFolder storageFolder = KnownFolders.CameraRoll;
        var file = (StorageFile) await storageFolder.TryGetItemAsync(imageName);
        uint width = 0, height = 0;
        if (file == null)
        {
            string errInfo = "MakeOCRRequest Error: Image File not exist." + imageName;
            Debug.Log(errInfo);
            ocrResult = errInfo;
            ocrStatus = SERVICE_STATUS.ERROR;
            return;
        }

        using (var stream = await file.OpenAsync(FileAccessMode.Read))
        {
            // Create a decoder from the stream. With the decoder, we can get the properties of the image.
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            double originalCroppedWidth = decoder.PixelWidth * widthRatio + 2;
            double originalCroppedHeight = decoder.PixelHeight * heightRatio + 2;
            // make sure the image is larger than 50x50 in real pixels.
            double scale = Math.Max(1.0f, Math.Max(AZURE_API_MIN_SIZE / originalCroppedWidth, AZURE_API_MIN_SIZE / originalCroppedHeight));

            uint startPointX = (uint)Math.Floor(decoder.PixelWidth * topLeft.x * scale) - 1;
            uint startPointY = (uint)Math.Floor(decoder.PixelHeight * topLeft.y * scale) - 1;
            width = (uint)Math.Floor(originalCroppedWidth * scale);
            height = (uint)Math.Floor(originalCroppedHeight * scale);

            // The scaledSize of original image.
            uint scaledWidth = (uint)Math.Floor(decoder.PixelWidth * scale);
            uint scaledHeight = (uint)Math.Floor(decoder.PixelHeight * scale);

            // Refine the start point and the size. 
            if (startPointX + width > scaledWidth)
            {
                startPointX = scaledWidth - width;
            }

            if (startPointY + height > scaledHeight)
            {
                startPointY = scaledHeight - height;
            }

            // Get the cropped pixels.
            BitmapTransform transform = new BitmapTransform();
            BitmapBounds bounds = new BitmapBounds();
            bounds.X = startPointX;
            bounds.Y = startPointY;
            bounds.Height = height;
            bounds.Width = width;
            transform.Bounds = bounds;

            transform.ScaledWidth = scaledWidth;
            transform.ScaledHeight = scaledHeight;

            // Get the cropped pixels within the bounds of transform.
            PixelDataProvider pix = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
            croppedBytes = pix.DetachPixelData();
        }

        // Again, I have to save to file stream
        /* byte[] to image.
         * https://code.msdn.microsoft.com/windowsapps/How-to-save-WriteableBitmap-bd23d455
         * */
        var tempFile = await storageFolder.CreateFileAsync("tempOCR.jpg", CreationCollisionOption.GenerateUniqueName);
        using (var stream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, width, height, 96, 96, croppedBytes);
            await encoder.FlushAsync();

            var reader = new DataReader(stream.GetInputStreamAt(0));
            byteData = new byte[stream.Size];
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(byteData);
        }
        await tempFile.DeleteAsync();

        using (ByteArrayContent content = new ByteArrayContent(byteData))
        {
            // This example uses content type "application/octet-stream".
            // The other content types you can use are "application/json" and "multipart/form-data".
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            // Execute the REST API call.
            response = await client.PostAsync(uri, content);

            // Get the JSON response.
            string contentString = await response.Content.ReadAsStringAsync();

            // Display the JSON response.
            Debug.Log("\nResponse:\n");
            Debug.Log(JsonPrettyPrint(contentString));

            // Parse to output the result.
            var result = JsonConvert.DeserializeObject<JSONOCR.RootObject>(contentString);
            var texts = new List<string>();
            foreach (var region in result.regions)
            {
                foreach (var line in region.lines)
                {
                    foreach (var word in line.words)
                    {
                        texts.Add(word.text);
                    }
                }
            }
            if (texts.Count > 0)
            {
                ocrResult = string.Join(" ", texts);
                Debug.Log("MakeOCRRequest succeeded:" + ocrResult);
                ocrStatus = SERVICE_STATUS.DONE;
            }
            else
            {
                string errInfo = "MakeOCRRequest succeeded but the result is empty.";
                ocrResult = errInfo;
                Debug.Log(errInfo);
                ocrStatus = SERVICE_STATUS.ERROR;
            }   
        }
    }
    
    /// <summary>
    /// Recognize the text from handwriting using Microsoft Azure service.
    /// </summary>
    public async void RecognizeInking(IReadOnlyList<InkStroke> strokeList, double pageWidth, double pageHeight)
    {
        // Current bounding box for the strokes.
        double tlX = double.MaxValue;
        double tlY = double.MaxValue;
        double brX = 0;
        double brY = 0;
        inkingStatus = SERVICE_STATUS.PENDING;
        // Make a copy of this list
        List<InkStroke> newList = new List<InkStroke>();
        foreach (InkStroke ss in strokeList)
        {
            newList.Add(ss);

            tlX = Math.Min(tlX, ss.BoundingRect.Left);
            tlY = Math.Min(tlY, ss.BoundingRect.Top);
            brX = Math.Max(brX, ss.BoundingRect.Right);
            brY = Math.Max(brY, ss.BoundingRect.Bottom);
        }
        double originalCroppedWidth = brX - tlX;
        double originalCroppedHeight = brY - tlY;
        // Create boundary
        tlX = Math.Max(0, tlX - originalCroppedWidth * 0.2);
        tlY = Math.Max(0, tlY - originalCroppedHeight * 0.4);
        brX = Math.Min(pageWidth, brX + originalCroppedWidth * 0.2);
        brY = Math.Min(pageHeight, brY + originalCroppedHeight * 0.4);
        originalCroppedWidth = brX - tlX;
        originalCroppedHeight = brY - tlY;

        StorageFolder storageFolder = KnownFolders.CameraRoll;
        var file = await storageFolder.CreateFileAsync("sampleInking.jpg", CreationCollisionOption.GenerateUniqueName);
        
        // Render a whole image (paper size * inking scale)
        CanvasDevice device = CanvasDevice.GetSharedDevice();
        CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (float)pageWidth, (float)pageHeight, 96);
        //await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
        //    () =>
        //    {
        //        using (var ds = renderTarget.CreateDrawingSession())
        //        {
        //            ds.Clear(Colors.White);
        //            ds.DrawInk(strokeList);
        //        }
        //    });
        using (var ds = renderTarget.CreateDrawingSession())
        {
            ds.Clear(Colors.White);
            ds.DrawInk(newList);
        }

        // Crop the image: using same algorithm as in OCR method.
        // croppedBytes: image bytes.
        // byteData: final format, with bmp header.
        byte[] byteData = new byte[1];
        byte[] croppedBytes = new byte[1];
        uint width = 0, height = 0;
        using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
        {
            await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Jpeg, 1f);
            //Debug.Log("Save to:" + file.Name);

            // Crop this image.
            // Create a decoder from the stream. With the decoder, we can get the properties of the image.
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);
            
            // make sure the image is larger than 50x50 in real pixels.
            double scale = Math.Max(1.0f, Math.Max(AZURE_API_MIN_SIZE / originalCroppedWidth, AZURE_API_MIN_SIZE / originalCroppedHeight));

            uint startPointX = (uint)Math.Floor(tlX * scale);
            uint startPointY = (uint)Math.Floor(tlY * scale);
            width = (uint)Math.Floor(originalCroppedWidth * scale);
            height = (uint)Math.Floor(originalCroppedHeight * scale);

            // The scaledSize of original image.
            uint scaledWidth = (uint)Math.Floor(decoder.PixelWidth * scale);
            uint scaledHeight = (uint)Math.Floor(decoder.PixelHeight * scale);

            // Refine the start point and the size. 
            if (startPointX + width > scaledWidth)
            {
                startPointX = scaledWidth - width;
            }

            if (startPointY + height > scaledHeight)
            {
                startPointY = scaledHeight - height;
            }

            // Get the cropped pixels.
            BitmapTransform transform = new BitmapTransform();
            BitmapBounds bounds = new BitmapBounds();
            bounds.X = startPointX;
            bounds.Y = startPointY;
            bounds.Height = height;
            bounds.Width = width;
            transform.Bounds = bounds;

            transform.ScaledWidth = scaledWidth;
            transform.ScaledHeight = scaledHeight;

            // Get the cropped pixels within the bounds of transform.
            PixelDataProvider pix = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.ColorManageToSRgb);
            croppedBytes = pix.DetachPixelData();
            //Debug.Log(string.Format("Crop Handwritten image: start: {0},{1}, width:{2}, height:{3}", bounds.X, bounds.Y, bounds.Width, bounds.Height));
        }
        await file.DeleteAsync();

        // Again, I have to save to file stream byte[] to image.
        // https://code.msdn.microsoft.com/windowsapps/How-to-save-WriteableBitmap-bd23d455
        var tempFile = await storageFolder.CreateFileAsync("temp-sampleInking.jpg", CreationCollisionOption.GenerateUniqueName);
        using (var stream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, width, height, 96, 96, croppedBytes);
            await encoder.FlushAsync();

            var reader = new DataReader(stream.GetInputStreamAt(0));
            byteData = new byte[stream.Size];
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(byteData);
        }
        await tempFile.DeleteAsync();

        //ReadHandwrittenText("");
        HttpClient client = new HttpClient();

        // Request headers.
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", azureKey);

        // Request parameter. Set "handwriting" to false for printed text.
        string requestParameters = "handwriting=true";

        // Assemble the URI for the REST API Call.
        string uri = azureUri + "recognizeText?" + requestParameters;

        HttpResponseMessage response = null;

        // This operation requrires two REST API calls. One to submit the image for processing,
        // the other to retrieve the text found in the image. This value stores the REST API
        // location to call to retrieve the text.
        string operationLocation = null;

        // Request body. Posts a locally stored JPEG image.
        //byte[] byteData = canvasImg;

        ByteArrayContent content = new ByteArrayContent(byteData);

        // This example uses content type "application/octet-stream".
        // You can also use "application/json" and specify an image URL.
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        // The first REST call starts the async process to analyze the written text in the image.
        response = await client.PostAsync(uri, content);

        // The response contains the URI to retrieve the result of the process.
        if (response.IsSuccessStatusCode)
            operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
        else
        {
            // Display the JSON error data.
            string errInfo = "RecognizeInking: PosyAsync Response Error." + response.StatusCode.ToString();
            inkingResult = errInfo;
            inkingStatus = SERVICE_STATUS.ERROR;
            Debug.Log(errInfo + "\n");
            //Debug.Log(JsonPrettyPrint(await response.Content.ReadAsStringAsync()));
            return;
        }

        // The second REST call retrieves the text written in the image.
        //
        // Note: The response may not be immediately available. Handwriting recognition is an
        // async operation that can take a variable amount of time depending on the length
        // of the handwritten text. You may need to wait or retry this operation.
        //
        // This example checks once per second for ten seconds.
        string contentString;
        int i = 0;
        do
        {
            await Task.Delay(1000);
            response = await client.GetAsync(operationLocation);
            contentString = await response.Content.ReadAsStringAsync();
            ++i;
        }
        while (i < 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1);

        if (i == 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1)
        {
            string errInfo = "RecognizeInking: Timeout Error.";
            inkingResult = errInfo;
            Debug.Log(errInfo + "\n");
            inkingStatus = SERVICE_STATUS.ERROR;
            return;
        }

        // Display the JSON response.
        //Debug.Log("\nResponse:\n");
        //Debug.Log(JsonPrettyPrint(contentString));
        // Parse to output the result.
        var result = JsonConvert.DeserializeObject<JSONInking.RootObject>(contentString).recognitionResult;
        var texts = new List<string>();
        foreach (var line in result.lines)
        {
            texts.Add(line.text);
        }
        
        if (texts.Count > 0)
        {
            inkingResult = string.Join(" ", texts);
            //Debug.Log("Inking Recognition succeeded:" + inkingResult);
            inkingStatus = SERVICE_STATUS.DONE;
        }
        else
        {
            string errInfo = "Inking Recognition succeeded but the result is empty.";
            inkingResult = errInfo;
            Debug.Log(errInfo);
            inkingStatus = SERVICE_STATUS.ERROR;
        }
    }

    public async void AcademicSearch(string text, int count = 30, int offset = 0)
    {
        academicStatus = SERVICE_STATUS.PENDING;

        HttpClient client = new HttpClient();

        // Request headers.
        client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", labsKey);

        var parameters = new List<string>();

        var textArray = text.Split(' ');
        List<string> newTextList = new List<string>();
        string expr = "expr=And(";
        for (int j = 0; j < textArray.Length; ++j)
        {
            string candidate = new string(textArray[j].Where(Char.IsLetterOrDigit).ToArray());
            if (candidate.Length > 1)
                newTextList.Add(string.Format("W=\'{0}\'", candidate.ToLower()));
        }
        if (newTextList.Count == 0)
        {
            academicResult = "AcademicSearch Error: Keywords not valid <" + text + ">.";
            //Debug.LogError(academicErrInfo);
            academicStatus = SERVICE_STATUS.ERROR;
            return;
        }
        expr = string.Format("expr=And({0})", string.Join(",", newTextList));
        //string backupExpr = string.Format("expr=Or({0})", string.Join(",", newTextArray));
        parameters.Add(expr);

        parameters.Add("model=latest");
        parameters.Add(string.Format("count={0}", count));
        parameters.Add(string.Format("offset={0}", offset));
        // Title, AuthorName, ConferenceName, JournalName, Year, Extra (include:DisplayName, resource type, resoure url)
        parameters.Add(string.Format("attributes=Ti,AA.AuN,Y,C.CN,J.JN,E"));

        // Request parameter. 
        string requestParameters = string.Join("&", parameters);

        // Assemble the URI for the REST API Call.
        string uri = labsUri + "evaluate?" + requestParameters;
        //Debug.Log("requested uri:" + uri);

        HttpResponseMessage response = null;

        response = await client.GetAsync(uri);
        //Debug.Log(response.StatusCode);
        //academicErrInfo = response.StatusCode.ToString();
        using (HttpContent content = response.Content)
        {
            Task<string> task = content.ReadAsStringAsync();
            string jsonResult = task.Result;
            Debug.Log("Got result from Academic API:" + jsonResult.Length);
            //academicResult = ParseJSONResults(jsonResult);
            //ParseJSONResults(jsonResult);
            academicResult = jsonResult;
            academicStatus = SERVICE_STATUS.DONE;
            //Debug.Log("Finish parsing in API Helper:" + academicResult.Count);
            // Display the JSON response.
            //Debug.Log("\nResponse:\n");
            //Debug.Log(JsonPrettyPrint(jsonResult));
        }
    }

    #region Static Helper Methods
    /// <summary>
    /// Returns the contents of the specified file as a byte array.
    /// </summary>
    /// <param name="imageFilePath">The image file to read.</param>
    /// <returns>The byte array of the image data.</returns>
    private static byte[] GetImageAsByteArray(string imageFilePath)
    {
        FileStream fileStream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read);
        BinaryReader binaryReader = new BinaryReader(fileStream);
        return binaryReader.ReadBytes((int)fileStream.Length);
    }

    /// <summary>
    /// Formats the given JSON string by adding line breaks and indents.
    /// </summary>
    /// <param name="json">The raw JSON string to format.</param>
    /// <returns>The formatted JSON string.</returns>
    private static string JsonPrettyPrint(string json)
    {
        if (string.IsNullOrEmpty(json))
            return string.Empty;

        json = json.Replace(Environment.NewLine, "").Replace("\t", "");

        string INDENT_STRING = "    ";
        var indent = 0;
        var quoted = false;
        var sb = new StringBuilder();
        for (var i = 0; i < json.Length; i++)
        {
            var ch = json[i];
            switch (ch)
            {
                case '{':
                case '[':
                    sb.Append(ch);
                    if (!quoted)
                    {
                        sb.AppendLine();
                        Enumerable.Range(0, ++indent).ForEach(item => sb.Append(INDENT_STRING));
                    }
                    break;
                case '}':
                case ']':
                    if (!quoted)
                    {
                        sb.AppendLine();
                        Enumerable.Range(0, --indent).ForEach(item => sb.Append(INDENT_STRING));
                    }
                    sb.Append(ch);
                    break;
                case '"':
                    sb.Append(ch);
                    bool escaped = false;
                    var index = i;
                    while (index > 0 && json[--index] == '\\')
                        escaped = !escaped;
                    if (!escaped)
                        quoted = !quoted;
                    break;
                case ',':
                    sb.Append(ch);
                    if (!quoted)
                    {
                        sb.AppendLine();
                        Enumerable.Range(0, indent).ForEach(item => sb.Append(INDENT_STRING));
                    }
                    break;
                case ':':
                    sb.Append(ch);
                    if (!quoted)
                        sb.Append(" ");
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }
        return sb.ToString();
    }
    #endregion
#endif
}

static class Extensions
{
    public static void ForEach<T>(this IEnumerable<T> ie, Action<T> action)
    {
        foreach (var i in ie)
        {
            action(i);
        }
    }
}

namespace JSONOCR
{
    public class Word
    {
        public string boundingBox { get; set; }
        public string text { get; set; }
    }

    public class Line
    {
        public string boundingBox { get; set; }
        public List<Word> words { get; set; }
    }

    public class Region
    {
        public string boundingBox { get; set; }
        public List<Line> lines { get; set; }
    }

    public class RootObject
    {
        public string language { get; set; }
        public string orientation { get; set; }
        public double textAngle { get; set; }
        public List<Region> regions { get; set; }
    }
}

namespace JSONInking
{
    public class Word
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
    }

    public class Line
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public List<Word> words { get; set; }
    }

    public class RecognitionResult
    {
        public List<Line> lines { get; set; }
    }

    public class RootObject
    {
        public string status { get; set; }
        public RecognitionResult recognitionResult { get; set; }
    }
}

namespace JSONAcademic
{
    public class AA
    {
        public string AuN { get; set; }
    }

    public class C
    {
        public string CN { get; set; }
    }

    public class J
    {
        public string JN { get; set; }
    }

    public class Entity
    {
        public double logprob { get; set; }
        public long Id { get; set; }
        public string Ti { get; set; }
        public int Y { get; set; }
        public List<AA> AA { get; set; }
        public C C { get; set; }
        public J J { get; set; }
        public string E { get; set; }
    }

    public class RootObject
    {
        public string expr { get; set; }
        public List<Entity> entities { get; set; }
    }
}