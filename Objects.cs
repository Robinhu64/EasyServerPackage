using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace objects
{
    public class Car
    {
        public static readonly Guid CarTypeGuid = new Guid("6F9619FF-8B86-D011-B42D-00CF4FC964FF");
        public string Model;
        public int Year;
        public string Color;

        public Car(string model, int year, string color)
        {
            Model = model;
            Year = year;
            Color = color;

        }
        public string CarInfo()
        {
            return "The " + Color + " " + Model + " is made in " + Year.ToString();
        }

    }
    public class Book
    {
        public static readonly Guid BookTypeGuid = new Guid("1D65CA82-8EA7-4F9C-8A23-282D5AE7A620");
        public string Author;
        public int Pages;

        public Book(string author, int pages)
        {
            Author = author;
            Pages = pages;
        }

    }

}
