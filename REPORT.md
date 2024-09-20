## The problem

I decided to make a web crawler as the final project for concurrent programming course.

Web crawlers, also known as spiders are most often used by search engines such as Google, Bing DuckDuckGo, to go through web pages and read the content on them. Web crawlers work by giving them an initial address to start from and from there it will find new addresses to browse through. Web crawlers benefit a lot from concurrency, since usually the highest amount of time is spent loading the page. By using some concurrent method such as using multiple threads to load and read through the pages, performance of the crawler can be substantially increased.

I did not create the crawler to work as a search engine, but more as a tool to map the file structure of web pages. 


## Implementation

I implemented the solution by creating a class named ``Crawler``.
The class packs inside itself 
* ``HttpClient`` to make requests
* ``ConcurrentQueue`` to hold urls to visit next
* ``HashSet`` for keeping track of all visited urls and urls found. Every url will not end in queue as crawler only wants to go through html, but for mapping all urls are nice to have. Why I used HashSet instead of List I will explain further down
* ``Semaphore`` for protecting variables from data races
* ``List`` of tasks that do all the work
* ``SiteMap`` (my own implementation) that holds recursive data structure to map out the web pages 

Crawler is started by calling Run method, which creates desired amount of Tasks.
``CancellationToken`` is also passed in case the user wants to stop the execution.

Each task will start by dequeing an url from the queue. If the queue is empty, task will try dequeing item until it will timeout (5 sec) or it will dequeue url succesfully. Queue uses ConcurrentQueue class so it is thread safe.

Url is then used to create a http request. First status code is checked to be ok and then the header content is checked to be text/html.

If this is ok, page is loaded and passed to helper function which finds all urls by looking for strings in the html which start with ``href="`` and end with ``"``.

These links are then added to a HashSet which is basically an unordered list. I will call it a list for now the keep things simple. Elements in this list are then added to a list which contains all urls, if it is not already in it, and it will also get added to queue if the url has not been yet visited and is not invalid file type such as .exe or .jpeg.

After parsing the urls from the body of html, the current url is added to a list which holds all visited urls.

After that, new url is dequeued and same process is repeated.

I originally used List for holding all urls, but I noticed that as the lists grew longer, operations got a lot slower because I had to check I do not insert duplicates into the list. So I thought about using dictionary and after doing some research I found HashSet which is kind of a dictionary with only keys.
In any case HashSet can check in O(1) time if duplicate exist when with List it takes O(n) so quite a big boost in performance was discovered there.

Also in my initial proposal I was thinking of making a class which holds filenames and folder names but in the end I realised it is quite useless to separate the two as it only made things whole lot more complex.

So this became 
```
class Folder 
{
    string Name;            // Name of the folder
    List<Folder> children;  // Child folders
    List<String> filenames; // Files of the current folder
}
```
this
```
class Folder 
{
    string Name;            // Name of the folder or file
    List<Folder> children;  // Children
}
```

The so called ``SiteMap`` is just a list of Folders which can be iterated through recursively to print out the site mapping.

I made the functions for checking and parsing the urls myself but I later thought there might have been a library for that and so there was a Uri class that would have probably made my work a bit faster but then again, I didn't mind creating my own implementations. 

My initial thought was to create multiple threads to do all the work but I soon changed my direction to using multiple tasks instead. I had some problems with the main thread finishing before the worker threads and as the network requests themself were async, tasks seemed like a better choice for this. Overall tasks are more efficient than threads and they are easier to use.

Mapping the urls is done with one thread and it can take quite a while when the list of urls starts getting big. With around 100 000 urls, my computer mapped it in around 20 seconds. Mapping can be done only after other tasks finish their work, so it is not exactly like I thought it to be in my proposal, but as the urls are held in HashSet, it is hard to iterate through it concurrently since HashSet is unordered. Of course it could be achieved, perhaps by moving the urls to a list and then sharing the work to multiple threads, but the current implementation works well enough for me.

I also made an GUI with WPF as a little bonus. I had to implement some threading in there too to make the UI not freeze. Crawler is started in its own thread and the mapping is also done in its own thread so the UI will stay responsive to the user. That didnt cause a lot of trouble, though I had to do some research on how to update the UI from other threads as updating it from another thread directly is not allowed.

![](./img/2.png)

### Some features

On the GUI user must give url to start crawling from.
User can set the amount of threads and the amount of requests each thread will do before stopping.

Multiple keywords can be added in case the user wants to map sites that contain those strings in the url. This is nice when you only want to map certain part of a site or dont want to leave it.

Url parameters can be chosen to be discarded which means these two urls

* http://example.com?q=example
* http://example.com?q=search

Will be treated as http://example.com.

User can set if he wants the crawler to look for files. This will make the crawler look for strings that start with ``src="`` in the html.

After running the queries, user can choose to create a map of the sites by pressing save button which will then save the map into a text file of given destination.

The map itself will have this sort of structure.
It would be quite simple to modify the program to output JSON if needed.
```
- https://student.labranet.jamk.fi
    - wp-content
        - themes
            - blankslate
                - style.css
        - plugins
            - nextend-smart-slider3-pro
                - library
                    - media
                        - smartslider.min.css?1536570312
                        - dist
                            - smartslider-frontend.min.js?1536570312
                            - particles.min.js?1536570312
                        - plugins
                            - type
                                - block
                                    - block
                                        - dist
                                            - smartslider-block-type-frontend.min.js?1536570312
                - nextend
                    - media
                        - dist
                            - n2-j.min.js?1536570312
                            - nextend-gsap.min.js?1536570312
                            - nextend-frontend.min.js?1536570312
                            - nextend-webfontloader.min.js?1536570312
```

I spent around 20 - 25 hours coding and testing the crawler itself and around 7 hours putting the UI together and adding some features to the crawler class to support updating the UI status.