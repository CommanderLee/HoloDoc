using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ARDocument {

    /// <summary>
    /// Unique id, represents the file.
    /// </summary>
    public string filename = "";

    // Meta-data for the paper
    public string title = "";
    public string[] authors;
    public string source;
    public int year;

    public string videoLink = "";
    public string pdfLink = "";
    public string extraInfo = "";
    public int hasPreview;
    public int hasFullDocs;
    public int hasFigures;

    /// <summary>
    /// Array of references, generated from online source.
    /// </summary>
    public string[] references;

    public ARDocument (string filename, string title, string[] authors, string source, int year, string pdf = "", string video = "")
    {
        this.filename = filename;
        this.title = title;
        this.authors = authors;
        this.source = source;
        this.year = year;

        //TODO: Get references online using semantic scholar API.
        //this.references = new string[] { "[Default] [1] W3C’s Editor/Browser, World Wide Web Consortium (W3C).", "T. Arai, D. Aust, and S. Hudson. PaperLink: A Technique for Hyperlinking from Real Paper to Electronic Content.In Proceedings of ACM CHI’97, Conference on Human Factors in Computing Systems, March 1997." };
        LoadReferences();

        this.pdfLink = pdf;
        this.videoLink = video;
        // Default: 0. N/A: -1. Available: 1.
        this.hasFullDocs = 0;
    }

    private void LoadReferences()
    {
        // TODO: find these using online API.
        if (this.filename == "Norrie03")
        {
            string[] newRef = new string[41 + 1];
            newRef[15] = "Johnson93";
            newRef[32] = "Sellen03";
            newRef[14] = "Logitech06";
            newRef[29] = "Paper00";
            newRef[27] = "OHara97";
            newRef[19] = "Levy01";
            newRef[28] = "OneNote07";
            newRef[25] = "Norrie02";
            this.references = newRef;
        }
        else if (this.filename == "OHara97")
        {
            this.references = new string[23 + 1];
        }
        else if (this.filename == "Johnson93")
        {
            this.references = new string[3 + 1];
        }
        else if (this.filename == "Klamka17")
        {
            string[] newRef = new string[67 + 1];
            newRef[35] = "Luff04";
            newRef[34] = "Luff07";
            newRef[17] = "Hansen88";
            newRef[55] = "Signer06";
            newRef[59] = "Steimle12";
            newRef[65] = "Wellner93";
            newRef[44] = "Norrie07";
            newRef[27] = "Lee04";
            newRef[28] = "Liao06";
            newRef[33] = "Lopes16";
            this.references = newRef;
        }
    }

    public override string ToString()
    {
        return string.Format("Title:<{0}>{1}\nAuthors:{2}\nSource:{3}-{4}\nPDF:{5}\nVideo:{6}\n", 
            filename, title, string.Join(", ", authors), source, year, pdfLink, videoLink);
    }

    //// Use this for initialization
    //void Start () {

    //}

    //// Update is called once per frame
    //void Update () {

    //}
}

enum AnotoMarkTypes
{
    REFERENCE,
    AUTHOR,
    FIGURE,
    TABLE,
    TITLE,
    PEN_CIRCLE
}

/// <summary>
/// Describe a page printed with Anoto patterns, and all marks on it
/// </summary>
class AnotoPage
{
    /// <summary>
    /// File name is also a unique ID across applications, e.g., "Norrie03".
    /// </summary>
    public string fileName;

    /// <summary>
    /// Local page number, count from 1
    /// </summary>
    public int page;

    /// <summary>
    /// All the marks in the document, based on different types and location
    /// </summary>
    public List<AnotoMark> marks;
    public AnotoPage(string fn, int pg)
    {
        fileName = fn;
        page = pg;
        marks = new List<AnotoMark>();
    }
}

/// <summary>
/// Entry of each anoto marks, with its location on the page
/// </summary>
class AnotoMark
{
    public AnotoMarkTypes type;

    /// <summary>
    /// The meaning of id depends on the type.
    /// E.g., it could be line 0, line 1 for the TITLE, author 0, author 1 for the AUTHOR (count from 0)
    /// or Figure 1, Table 2, Reference 44, etc. (count from 1 or based on the real paper)
    /// </summary>
    public int id;

    public int x;
    public int y;
    /// <summary>
    /// Other information. E.g., the target word for Pen-circle selection.
    /// </summary>
    public string other;

    public AnotoMark(AnotoMarkTypes type, int id, int x, int y, string other = "")
    {
        this.type = type;
        this.id = id;
        this.x = x;
        this.y = y;
        this.other = other;
    }
}

// Not useful anymore: invovled in the ARDocument class
//class MyDocument
//{
//    public string title;
//    public string[] authors;
//    public string[] references;
//    public bool updateFlag;

//    public MyDocument(string title)
//    {
//        this.title = title;
//        authors = null;
//        references = null;
//        updateFlag = false;
//    }
//}
