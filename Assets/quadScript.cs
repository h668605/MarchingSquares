﻿using UnityEngine;
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

    Texture2D _texture;


    private Button _button;
    private Toggle _toggle;
    private Slider _slider1;
    //int _iso;


    // Use this for initialization
    void Start()
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _button = uiDocument.rootVisualElement.Q("button1") as Button;
        _toggle = uiDocument.rootVisualElement.Q("toggle") as Toggle;
        _slider1 = uiDocument.rootVisualElement.Q("slider1") as Slider;
        _button.RegisterCallback<ClickEvent>(button1Pushed);
        _slider1.RegisterValueChangedCallback(slicePosSliderChange);

        Slice.initDicom();


        string
            dicomfilepath =
                Application.dataPath +
                @"\..\dicomdata\"; // Application.dataPath is in the assets folder, but these files are "managed", so we go one level up

        _slices = processSlices(dicomfilepath); // loads slices from the folder above
        setTexture(_slices[0], 200, 0.50f); // shows the first slice
        //createCircle(20);

        /***

        //  gets the mesh object and uses it to create a diagonal line
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

        */
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

    void setTexture(Slice slice, float radius, float threshold)
    {
        int step = 5;
        int xdim = slice.sliceInfo.Rows;
        int ydim = slice.sliceInfo.Columns;

        int[][] gridArray = new int[xdim][];


        var texture =
            new Texture2D(xdim, ydim, TextureFormat.RGB24, false); // garbage collector will tackle that it is new'ed 

        ushort[] pixels = slice.getPixels();


        for (int y = 0; y < ydim; y++)
        {
            for (int x = 0; x < xdim; x++)
            {
                float distance = Mathf.Sqrt(Mathf.Pow((x - (xdim / 2)), 2) + Mathf.Pow((y - (ydim / 2)), 2));

                float t = Mathf.Clamp01(distance / radius);

                texture.SetPixel(x, y, new UnityEngine.Color(t, t, t));
            }
        }


        texture.filterMode =
            FilterMode.Point; // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply(); // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = texture;

        _texture = texture;

        MarchingSquares(step, threshold);
    }

    void MarchingSquares(int step, float threshold)
    {
        // Hopp med 10 for eksempel
        // Mål de fire verdiene mot hverandre i en binær streng.
        // Lag cases for alle mulige linjer
        // Legg til i indice og vertice array
        // Kall på tegneprogram


        //  gets the mesh object and uses it to create a diagonal line
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();



        Debug.Log(_texture.height);


        for (int y = 0; y < _texture.height; y = y + step)
        {
            for (int x = 0; x < _texture.height; x = x + step)
            {
                float topLeft = _texture.GetPixel(x, y).grayscale;
                float topRight = _texture.GetPixel(x + step, y).grayscale;
                float bottomLeft = _texture.GetPixel(x, y + step).grayscale;
                float bottomRight = _texture.GetPixel(x + step, y + step).grayscale;


                int caseValue = (topLeft < threshold ? 8 : 0) |
                                (topRight < threshold ? 4 : 0) |
                                (bottomRight < threshold ? 2 : 0) |
                                (bottomLeft < threshold ? 1 : 0);


                // Define cell corner positions
                Vector3 p1 = OversettKoordinat(x, y);
                Vector3 p2 = OversettKoordinat(x + step, y);
                Vector3 p3 = OversettKoordinat(x + step, y + step);
                Vector3 p4 = OversettKoordinat(x, y + step);

                Vector3 midTop = (p1 + p2) / 2;
                Vector3 midRight = (p2 + p3) / 2;
                Vector3 midBottom = (p3 + p4) / 2;
                Vector3 midLeft = (p4 + p1) / 2;


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

        DrawLine(vertices, indices, mscript);
    }

    void DrawLine(List<Vector3> vertices, List<int> indices, meshScript mscript)
    {
        
        List<Vector3> vert = new List<Vector3>();
        List<int> ind = new List<int>();
        vert.Add(new Vector3(-0.5f, -0.5f, 0));
        vert.Add(new Vector3(0.5f, 0.5f, 0));
        vert.Add(new Vector3(1.0f, 1.0f, 0));
        ind.Add(0);
        ind.Add(1);
        ind.Add(1);
        ind.Add(2);
        //mscript.createMeshGeometry(vert, ind);
        
        mscript.createMeshGeometry(vertices, indices);
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
            //indices.Add(vertices.Count - 3);
            //indices.Add(vertices.Count - 2);   
            indices.Add(vertices.Count - 2);
            indices.Add(vertices.Count - 1);
        }
    }

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
        setTexture(_slices[0],evt.newValue*2,0.50f);
    }

    public void sliceIsoSliderChange(float val)
    {
        print("sliceIsoSliderChange:" + val);
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