using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;

namespace CaidaoBruter
{
    class FormHelper
    {
        public static string GetCheckedRadioButton(Control.ControlCollection controlCollection)
        {
            foreach (var x in controlCollection)
            {
                if (x is RadioButton)
                {
                    RadioButton radioButton = x as RadioButton;
                    if (radioButton.Checked)
                    {
                        return radioButton.Text.Trim();
                    }
                }
            }
            return null;
        }
        
        public static char[] GetSelectedChars(Control.ControlCollection controlCollection)
        {
            List<string> l = new List<string>();
            foreach (var x in controlCollection)
            {
                if (x is CheckBox)
                {
                    CheckBox checkBox = x as CheckBox;
                    if (checkBox.Checked)
                    {
                        l.Add(checkBox.Tag.ToString().Trim());
                    }
                }
            }
            if (l.Count == 0) return null;

            List<char> charsList = new List<char>();
            foreach (var x in l)
            {
                switch (x)
                {
                    case "num":
                        for (char c = '0'; c <= '9'; ++c) charsList.Add(c);
                        break;

                    case "lowerAlpha":
                        for (char c = 'a'; c <= 'z'; ++c) charsList.Add(c);
                        break;

                    case "upperAlpha":
                        for (char c = 'A'; c <= 'Z'; ++c) charsList.Add(c);
                        break;
                }
            }
            return charsList.ToArray();

        }
        // 利用迭代器生成字典
        public static IEnumerable<string> GenerateDict(char[] chars, int i, int n)
        {
            if(i == n)
            {
                Console.WriteLine("test1");
                yield return string.Join("", chars);
            }
            for(int j = i; j < chars.Length; ++j)
            {
                Swap<char>(ref chars[i], ref chars[j]);
                foreach (var result in GenerateDict(chars, i + 1, n)) yield return result;
                Swap<char>(ref chars[i], ref chars[j]);
            }
        }



        public static void Swap<T>(ref T a, ref T b)
        {
            T x = a;
            a = b;
            b = x;
        }
        public static void ControlInvokeSafe(Control c, Action a)
        {
            if (c.InvokeRequired) c.Invoke(new MethodInvoker(() => a()));
            else a();
        }
        public static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
    }

    public static class ArraySlice
    {
        public static T[] Slice<T>(this T[] source, int start, int end)
        {
            // Handles negative ends.
            if (end < 0)
            {
                end = source.Length + end;
            }
            int len = end - start;

            // Return new array.
            T[] res = new T[len];
            for (int i = 0; i < len && i < source.Length ; i++)
            {
                res[i] = source[i + start];
            }
            return res;
        }

    }
}
