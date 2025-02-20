using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.VersionControl;

public class quadScript : MonoBehaviour
{
    // Dicom har et "levende" dictionary som leses fra xml ved initDicom
    // slices må sorteres, og det basert på en tag, men at pixeldata lesing er en separat operasjon, derfor har vi nullpeker til pixeldata
    // dicomfile lagres slik at fil ikke må leses enda en gang når pixeldata hentes

    // member variables of quadScript, accessible from any function
    Slice[] _slices;
    int _numSlices;
    int _minIntensity;
    int _maxIntensity;
    float _radius;
    private float _threshold;
    private float _slider2Value;
    int _step;

    Texture2D _texture;


    private Button _button;
    private Toggle _toggle;
    private Slider _slider1;
    private Slider _slider2;
    //int _iso;


    // Use this for initialization
    void Start()
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _button = uiDocument.rootVisualElement.Q("button1") as Button;
        _toggle = uiDocument.rootVisualElement.Q("toggle") as Toggle;
        _slider1 = uiDocument.rootVisualElement.Q("slider1") as Slider;
        _slider2 = uiDocument.rootVisualElement.Q("slider2") as Slider;
        _button.RegisterCallback<ClickEvent>(button1Pushed);
        _slider1.RegisterValueChangedCallback(slicePosSliderChange);
        _slider2.RegisterValueChangedCallback(sliceIsoSliderChange);


        Slice.initDicom();


        string
            dicomfilepath =
                Application.dataPath +
                @"\..\dicomdata\"; // Application.dataPath is in the assets folder, but these files are "managed", so we go one level up

        _step = 10;
        _radius = 90000; // så høy siden det er 3D, tror jeg....
        _threshold = 0.5f;
        _slices = processSlices(dicomfilepath); // loads slices from the folder above
        setTexture(_slices[0]); // shows the first slice
        //createCircle(20);

        /***

        //gets the mesh object and uses it to create a diagonal line
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        vertices.Add(new Vector3(-0.5f, -0.5f, 0));
        vertices.Add(new Vector3(0.5f, 0.5f, 0));
        vertices.Add(new Vector3(1.0f, 1.0f, 0));
        indices.Add(0);
        indices.Add(1);
        indices.Add(1);
        indices.Add(2);
        mscript.createMeshGeometry(vertices, indices);
**/
    }


    Slice[] processSlices(string dicomfilepath)
    {
        string[] dicomfilenames = Directory.GetFiles(dicomfilepath, "*.IMA");
        _numSlices = dicomfilenames.Length;

        Slice[] slices = new Slice[_numSlices];

        float max = -1;
        float min = 99999;
        for (int i = 0; i < _numSlices; i++)
        {
            string filename = dicomfilenames[i];
            slices[i] = new Slice(filename);
            SliceInfo info = slices[i].sliceInfo;
            if (info.LargestImagePixelValue > max) max = info.LargestImagePixelValue;
            if (info.SmallestImagePixelValue < min) min = info.SmallestImagePixelValue;
            // Del dataen på max før den settes inn i tekstur
            // alternativet er å dele på 2^dicombitdepth,  men det ville blitt 4096 i dette tilfelle
        }

        print("Number of slices read:" + _numSlices);
        print("Max intensity in all slices:" + max);
        print("Min intensity in all slices:" + min);

        _minIntensity = (int)min;
        _maxIntensity = (int)max;
        //_iso = 0;

        Array.Sort(slices);

        return slices;
    }

    void setTexture(Slice slice)
    {
        int xdim = slice.sliceInfo.Rows;
        int ydim = slice.sliceInfo.Columns;
        var texture =
            new Texture2D(xdim, ydim, TextureFormat.RGB24, false); // garbage collector will tackle that it is new'ed 


        for (int y = 0; y < ydim; y++)
        {
            for (int x = 0; x < xdim; x++)
            {
                //float distance = Mathf.Sqrt(Mathf.Pow((x - (xdim / 2)), 2) + Mathf.Pow((y - (ydim / 2)), 2));
                //float t = Mathf.Clamp01(distance / _radius);

                float distance = (float)(Math.Pow(x - 256, 2) + Math.Pow(y - 256, 2) + Math.Pow(_slider2Value, 2));
                float t = Mathf.Clamp01(distance / _radius); 
                texture.SetPixel(x, y, new UnityEngine.Color(t, t, t));
                
            }
        }


        texture.filterMode =
            FilterMode.Point; // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply(); // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = texture;

        _texture = texture;

        MarchingSquares();
    }

    void MarchingSquares()
    {
        //Oppretter tegnefunskjon, og lister for vertices og indices
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();

        for (int y = 0; y < _texture.height; y = y + _step)
        {
            for (int x = 0; x < _texture.height; x = x + _step)
            {
                float topLeft = _texture.GetPixel(x, y).grayscale;
                float topRight = _texture.GetPixel(x + _step, y).grayscale;
                float bottomLeft = _texture.GetPixel(x, y + _step).grayscale;
                float bottomRight = _texture.GetPixel(x + _step, y + _step).grayscale;


                int caseValue = (topLeft < _threshold ? 8 : 0) |
                                (topRight < _threshold ? 4 : 0) |
                                (bottomRight < _threshold ? 2 : 0) |
                                (bottomLeft < _threshold ? 1 : 0);


                // Define cell corner positions
                Vector3 p1 = OversettKoordinat(x, y);
                Vector3 p2 = OversettKoordinat(x + _step, y);
                Vector3 p3 = OversettKoordinat(x + _step, y + _step);
                Vector3 p4 = OversettKoordinat(x, y + _step);

                Vector3 midTop = Interpolate(p1, p2, topLeft, topRight);
                Vector3 midRight = Interpolate(p2, p3, topRight, bottomRight);
                Vector3 midBottom = Interpolate(p3, p4, bottomRight, bottomLeft);
                Vector3 midLeft = Interpolate(p4, p1, bottomLeft, topLeft);


                switch (caseValue)
                {
                    case 1: AddLine(vertices, indices, midLeft, midBottom); break;
                    case 2: AddLine(vertices, indices, midBottom, midRight); break;
                    case 3: AddLine(vertices, indices, midLeft, midRight); break;
                    case 4: AddLine(vertices, indices, midTop, midRight); break;
                    case 5:
                        AddLine(vertices, indices, midTop, midLeft);
                        AddLine(vertices, indices, midBottom, midRight);
                        break;
                    case 6: AddLine(vertices, indices, midTop, midBottom); break;
                    case 7: AddLine(vertices, indices, midTop, midLeft); break;
                    case 8: AddLine(vertices, indices, midTop, midLeft); break;
                    case 9: AddLine(vertices, indices, midTop, midBottom); break;
                    case 10:
                        AddLine(vertices, indices, midTop, midRight);
                        AddLine(vertices, indices, midBottom, midLeft);
                        break;
                    case 11: AddLine(vertices, indices, midTop, midRight); break;
                    case 12: AddLine(vertices, indices, midLeft, midRight); break;
                    case 13: AddLine(vertices, indices, midBottom, midRight); break;
                    case 14: AddLine(vertices, indices, midLeft, midBottom); break;
                }
            }
        }

        mscript.createMeshGeometry(vertices, indices);
    }


    Vector3 Interpolate(Vector3 p1, Vector3 p2, float v1, float v2)
    {
        if (Mathf.Abs(v1 - v2) < 0.0001f)
            return (p1 + p2) / 2;
        float t = (_threshold - v1) / (v2 - v1);
        return p1 + t * (p2 - p1);
    }


    void AddLine(List<Vector3> vertices, List<int> indices, Vector3 p1, Vector3 p2)
    {
        if (vertices.Count == 0)
        {
            vertices.Add(p1);
            vertices.Add(p2);

            indices.Add(0);
            indices.Add(1);
        }
        else
        {
            vertices.Add(p1);
            vertices.Add(p2);

            indices.Add(vertices.Count - 2);
            indices.Add(vertices.Count - 1);
        }
    }

    //Oversetter koordinater fra teksturkoordinatsystem, til linjetegne system
    Vector3 OversettKoordinat(int x, int y)
    {
        float linjeX = (float)((x / (float)_texture.height) - 0.5);
        float linjeY = (float)((y / (float)_texture.height) - 0.5);
        Vector3 v = new Vector3(linjeX, linjeY, 0.0f);
        return v;
    }

    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }


    Vector2 vec2(float x, float y)
    {
        return new Vector2(x, y);
    }


    // Update is called once per frame
    void Update()
    {
    }

    public void slicePosSliderChange(ChangeEvent<float> evt)
    {
        print("slicePosSliderChange:" + evt.newValue);

        _threshold = evt.newValue / 100;
        setTexture(_slices[0]);
    }

    public void sliceIsoSliderChange(ChangeEvent<float> val)
    {
        print("sliceIsoSliderChange:" + val);

        _slider2Value = val.newValue;
        setTexture(_slices[0]);
    }

    public void button1Pushed(ClickEvent evt)
    {
        print("button1Pushed");
    }

    public void button2Pushed()
    {
        print("button2Pushed");
    }
}

/**
float rad = (float)x / (float)xdim;
float val = pixelval(new Vector2(x, y), xdim, pixels);
float v = (val-_minIntensity) / _maxIntensity;      // maps [_minIntensity,_maxIntensity] to [0,1] , i.e.  _minIntensity to black and _maxIntensity to white
texture.SetPixel(x, y, new UnityEngine.Color(rad, v, v));
**/