using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class updateImage : MonoBehaviour {

    public InputField path;

    public string JpegUrl;
    public string UserName;
    public string Password;
    private string b64Pass = "";

    Texture2D t;

    Renderer myRendere;

    string fullUrl;
    public string ipCamera = "10.0.0.6";
    Assets.Digest  d;

    void Start()
    {
        t = new Texture2D(4, 4, TextureFormat.DXT1, false);
        myRendere  = GetComponent<Renderer>();
        b64Pass = ToB64(Password);

        fullUrl = "http://" + ipCamera + ":8080/stream/video/mjpeg";

        Request1_NoAuth = @"GET /stream/video/mjpeg HTTP/1.1
Host: " + ipCamera + @":8080
User-Agent: Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/57.0.2987.133 Safari/537.36

";

        Request2_WithAuth = @"GET /stream/video/mjpeg HTTP/1.1
Host: " + ipCamera + @":8080
Connection: keep-alive
Cache-Control: max-age=0
Authorization: {0}
Accept-Encoding: gzip, deflate, sdch
Accept-Language: he-IL,he;q=0.8,en-US;q=0.6,en;q=0.4

";

        d = new Assets.Digest("http://" + ipCamera + ":8080", UserName, b64Pass); ;

        //StartCoroutine("downloadImage");
        StartCoroutine("SendWebRequestAsync");
    }

    // Update is called once per frame
    void Update ()
    {
        myRendere.material.mainTexture = t;
        
    }

    public string ToB64(string plain)
    {
        return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plain));
    }

    public void CopyTo2(Stream source, Stream destination)
    {
        byte[] buffer = new byte[1024]; 
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, bytesRead);
        }
    }

    public byte[] streamToArr(Stream source, int length)
    {
        byte[] result = new byte[length+1];
        source.Read(result, 0, length+1);

        return result;
    }
    
    IEnumerator downloadImage()
    {
        while (true)
        {
            yield return new WaitForSeconds(0);

            try
            {
                WebRequest request = WebRequest.Create(
                    "http://" +  path.text + ":8080/stream/snapshot.jpg"
                    );
                request.Credentials = new NetworkCredential(UserName, b64Pass);

                using (var response = request.GetResponse())
                {
                    //t.LoadImage(streamToArr(response.GetResponseStream(), (int)response.ContentLength));


                    using (MemoryStream ms = new MemoryStream())
                    {
                        CopyTo2(response.GetResponseStream(), ms);
                    
                        t.LoadImage(ms.ToArray());
                    
                    
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log("ex: " + ex.Message + ex.StackTrace);
            }

        }
    }

   


    //*************************************
    //*************** New Method:
    //*************************************

    string Request1_NoAuth;
    string Request2_WithAuth;


    TcpClient tc;
    NetworkStream ns;


    byte[] myBuffer = new byte[1024 * 1024]; // 1 1MB
    IEnumerator SendWebRequestAsync()
    {
        tc = new TcpClient();

        tc.Connect(ipCamera, 8080);
        ns = tc.GetStream();

        var sw = new System.IO.StreamWriter(ns);

        //using (var sr = new System.IO.StreamReader(ns))
        //{
        string DigestAnswer = d.GrabResponse("/stream/video/mjpeg");
        Debug.Log("Got digest:\n" + DigestAnswer);

        Debug.Log("Sending request...");
        string req = Request2_WithAuth.Replace("{0}", DigestAnswer);// String.Format(Request2_WithAuth, DigestAnswer);
        sw.Write(req);
        sw.Flush();

        Debug.Log("Getting Response...");

        // Read main headers:
        string allHeaders = readUntilEndHeaders(ns);
        Debug.Log("[*] Main Headers:\n" + allHeaders);

        string myBoundary = "--" + findBetween(allHeaders,"=","--") + "--";


        // Read each FRAME:
        while (true)
        {
            readUntilBuffer(ns, myBoundary);

            string allMySubHeaders = readUntilEndHeaders(ns);
            //Debug.Log("************ JPEG Headers:\n\n" + allMySubHeaders);

            // Analyze JPEG:
            int length =
                int.Parse(
                    findBetween(allMySubHeaders, "Content-Length: ", "\r\n")
            );

            getJPEG(ns, length);

            yield return new WaitForEndOfFrame();

        }


    }


    string readUntilEndHeaders(Stream stream)
    {
        string result = "";
        while (!result.EndsWith("\r\n\r\n"))
        {
            result += (char)stream.ReadByte();
        }

        return result;
    }


    void readUntilBuffer(Stream stream, string myBuffer)
    {
        string buffer = "";
        while (!buffer.EndsWith(myBuffer))
        {
            buffer += (char)stream.ReadByte();
            if (buffer.Length > myBuffer.Length)
                buffer = buffer.Substring(buffer.Length - myBuffer.Length, myBuffer.Length);
        }
    }

    void getJPEG(Stream stream, long length)
    {
        byte[] data = new byte[length];
        int total = 0;
        while (total < length)
        {
            total += stream.Read(data, total, (int)length - total);
        }

        t.LoadImage(data);

        //using (MemoryStream ms = new MemoryStream(data))
        //{
        //    t.LoadImage(ms.GetBuffer());
        //}

    }

    string findBetween(string source, string start, string end) {
        string[] data = source.Split(new[] { start },StringSplitOptions.RemoveEmptyEntries);
        if (data.Length == 2)
        {
            data = data[1].Split(new[] { end }, StringSplitOptions.RemoveEmptyEntries);
            return data[0];
        }
        return "";

    }

    
}
