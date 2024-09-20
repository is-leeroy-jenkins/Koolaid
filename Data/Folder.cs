using System.Collections.Generic;
using System.Text;

namespace WebCrawler
{
    class Folder
    {
        public string Name;
        public List<Folder> children;

        public Folder()
        {
            children = new List<Folder>();
        }

        public Folder(string name)
        {
            Name = name;
            children = new List<Folder>();
        }

        public void Insert(string url)
        {
            if (url == "")
            {
                return;
            }

            string[] strings = url.Split('/');
            string next = BuildNext(strings);

            bool isInserted = false;
            foreach (var folder in children)
            {
                if (folder.Name == strings[0])
                {
                    folder.Insert(next);
                    isInserted = true;
                }
            }

            if (!isInserted)
            {
                var folder = new Folder(strings[0]);
                folder.Insert(next);
                children.Add(folder);
            }
        }

        private string BuildNext(string[] strings)
        {
            string next = "";

            for (int i = 1; i < strings.Length; i++)
            {
                next += strings[i];
                if (i < strings.Length - 1)
                {
                    next += "/";
                }
            }

            return next;
        }

        public StringBuilder BuildString(int depth = 0)
        {
            StringBuilder str = new StringBuilder();

            for (int i = 0; i < depth; i++)
            {
                str.Append("    ");
            }

            str.Append("- " + Name + '\n');

            foreach (var item in children)
            {
                str.Append(item.BuildString(depth + 1));
            }

            return str;
        }
    }

}
