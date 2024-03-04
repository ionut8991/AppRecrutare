using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Validation;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace AppRecrutare
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //conectare la baza de date
            RecrutareEntities db = new RecrutareEntities();

            //citire nume job
            Console.Write("Numele jobului: ");
            var citirejob = Console.ReadLine();
            job numeJob = new job();
            numeJob.j_name = citirejob;
            //verificam daca exista in baza de date
            if (!db.jobs.Any(c => c.j_name == numeJob.j_name))
            {
                
                db.jobs.Add(numeJob);
                db.SaveChanges();
                Console.WriteLine("Job adaugat cu succes!");
            }
            else
            {
                Console.WriteLine("Exista deja jobul!");
            }



            //optiuni pentru driver si deschidere pagina
            ChromeOptions options = new ChromeOptions();
            options.AddArguments("start-maximized");
            options.AddUserProfilePreference("intl.accept_languages", "ro");
            options.AddUserProfilePreference("charset", "UTF-8");
            ChromeDriver driver = new ChromeDriver(options);


            //functia care salveaza joburile in baza de date 
            //BestJobsSript(driver, db, citirejob);
            EJobsSript(driver, db, citirejob);
            //inchidere driver 
            driver.Quit();
            //functia de export pdf
            ExportPDF();

        }

        //functie pentru a verifica daca exista un anumit element pe pagina
        static bool IsElementPresent(ChromeDriver driver, By by)
        {
            try
            {
                driver.FindElement(by);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }

        //functie pentru scroll pana la cel mai jos punct din pagina (fara a da click pe butonul de view more)
        static void ScrollDown(ChromeDriver driver)
        {
            bool scrolling = true;
            long lastHeight = 0;

            while (scrolling)
            {

                ((IJavaScriptExecutor)driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                Thread.Sleep(1000);


                long newHeight = (long)((IJavaScriptExecutor)driver).ExecuteScript("return document.body.scrollHeight");


                if (newHeight == lastHeight)
                {
                    scrolling = false;
                }
                else
                {
                    lastHeight = newHeight;
                }
            }
        }

        //functia de salvare a joburilor in baza de date
        static void BestJobsSript(ChromeDriver driver, RecrutareEntities db, string citirejob)
        {
            driver.Navigate().GoToUrl(@"https://www.bestjobs.eu/ro/locuri-de-munca/" + citirejob);
            Thread.Sleep(5000);
            //acceptare cookies
            driver.FindElement(By.ClassName("js-accept-cookie-policy")).Click();
            Thread.Sleep(5000);


            //functie pentru scroll si optionale pentru a incarca toate joburile de pe pagina (dureaza mult) sau doar cele de pe prima pagina
            try
            {
                bool viewMoreExists = true;
                while (viewMoreExists)
                {
                    ScrollDown(driver);

                    driver.FindElement(By.CssSelector(".js-show-next-page")).Click();

                    /*
                    Thread.Sleep(2000);  //Daca eliminam aceste comment-uri, se va face scroll pana jos de tot si se va da click pe butonul de view more pana se incarca absolut toate joburile -- folosit pentru prima data cand scriem numele unui job
                    ScrollDown(driver);  //Daca nu, se va face scroll pana jos de tot si se va da click pe butonul de view more doar o data, incarcandu-se doar joburile de pe prima pagina -- folosit pentru a verifica daca exista joburi noi
                    */
                    viewMoreExists = IsElementPresent(driver, By.CssSelector(".js-show-next-page"));

                }


            }
            catch
            {

            }

            IList<IWebElement> Joburi = driver.FindElements(By.CssSelector(".card.h-100.list-card.transition-box-shadow.transition-3d-hover.fast-apply-elements.border-0"));
            foreach (IWebElement elem in Joburi)
            {
                //cod pentru extragerea datelor referitoare la firma, post, locatie, salariu si link
                IWebElement ElementFirma = elem.FindElement(By.CssSelector(".h6.text-muted.text-truncate.py-2"));
                IWebElement FirmaDiv = ElementFirma.FindElement(By.TagName("small"));
                string firma = FirmaDiv.Text;

                IWebElement ElementPost = elem.FindElement(By.CssSelector(".h6.truncate-2-line"));
                IWebElement PostDiv = ElementPost.FindElement(By.TagName("strong"));
                string post = PostDiv.Text;

                string locatie = "Remote";
                IWebElement LocatieDiv = elem.FindElement(By.CssSelector("div.d-flex.min-width-3"));
                try
                {
                    locatie = LocatieDiv.FindElement(By.TagName("span")).GetAttribute("data-original-title");
                }
                catch
                {

                }

                IWebElement Salariu = elem.FindElement(By.CssSelector(".text-right"));
                string salariu = "";
                if (!string.IsNullOrEmpty(Salariu.Text))
                {
                    salariu = Salariu.FindElement(By.CssSelector(".text-nowrap")).Text;
                }

                string platforma = "BestJobs";

                string link = elem.FindElement(By.CssSelector("div[style='height: 0;']")).FindElement(By.TagName("a")).GetAttribute("href");

                //salvare in baza de date a joburilor
                anunturi anunt = new anunturi();
                anunt.a_jid = Convert.ToInt32((from j in db.jobs where j.j_name == citirejob select j.j_id).Single()); //atribuire id job pe baza numelui jobului introdus 
                anunt.a_firma = firma;
                anunt.a_post = post;
                anunt.a_locatie = locatie;
                anunt.a_salariu = salariu;
                anunt.a_platf = platforma;
                anunt.a_date = DateTime.Now;
                anunt.a_link = link;

                try
                {
                    //salvarea efectiva in baza de date prin SaveChanges
                    if (!db.anunturis.Any(c => c.a_link == anunt.a_link))
                    {
                        Console.WriteLine("Firma: " + firma + " Post: " + post + " Locatia: " + locatie + " Salariu: " + salariu + " Platforma: " + platforma + "\nLink: " + link);
                        db.anunturis.Add(anunt);
                        db.SaveChanges();
                    }
                }
                catch (DbEntityValidationException e)
                {
                    //eroare generata automat pentru a vedea exact ce anume nu functioneaza in baza de date
                    foreach (var eve in e.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                                                       eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                                                               ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                }

                anunt = null;
            }


        }


        static void EJobsSript(ChromeDriver driver, RecrutareEntities db, string citirejob)
        {
            driver.Navigate().GoToUrl(@"https://www.ejobs.ro/locuri-de-munca/salarii/" + citirejob);
            Thread.Sleep(3000);
            //acceptare cookies
            driver.FindElement(By.CssSelector(".CookiesPopup__AcceptButton.eButton.eButton--Primary")).Click();
            Thread.Sleep(3000);


            //functie pentru scroll si optionale pentru a incarca toate joburile de pe pagina (dureaza mult) sau doar cele de pe prima pagina
            //try
            //{
            //    //bool loadAllJobs = true; // Setează la true dacă vrei să încarci toate joburile

            //    bool viewMoreExists = true;
            //    while (viewMoreExists)
            //    {
            //        ScrollDown(driver);

            //        //driver.FindElement(By.CssSelector(".JLPButton.JLPButton--Next")).Click();

            //        //Thread.Sleep(10000);

            //        //ScrollDown(driver);                 

            //        viewMoreExists = IsElementPresent(driver, By.CssSelector(".JLPButton.JLPButton--Next"));
            //    }
            //}
            //catch
            //{
            //    // Gestionarea excepțiilor
            //}

            ScrollDown(driver);

            IList<IWebElement> Joburi = driver.FindElements(By.CssSelector("JCContent"));
            foreach (IWebElement elem in Joburi)
            {
                //cod pentru extragerea datelor referitoare la firma, post, locatie, salariu si link
                IWebElement ElementFirma = elem.FindElement(By.CssSelector(".JCContentMiddle__Info--Darker"));
                IWebElement FirmaDiv = ElementFirma.FindElement(By.TagName("a"));
                string firma = FirmaDiv.Text;

                IWebElement ElementPost = elem.FindElement(By.CssSelector(".JCContentMiddle__Title"));
                IWebElement PostDiv = ElementPost.FindElement(By.TagName("span"));
                string post = PostDiv.Text;

                IWebElement ElementLocatie = elem.FindElement(By.CssSelector(".JCContentMiddle"));
                IWebElement LocatieDiv = ElementLocatie.FindElement(By.XPath("//span[@class='JCContentMiddle__Info']"));               
                string locatie = LocatieDiv.Text;

                IWebElement ElementSalariu = elem.FindElement(By.CssSelector(".JCContentMiddle__Info"));
                //IWebElement SalariuDiv = ElementSalariu.FindElement(By.XPath("//div[@class='JCContentMiddle__Info' and @data-v-d787945c='']"));
                string salariu = ElementSalariu.Text;
                
                string platforma = "EJobs";

                string link = elem.FindElement(By.CssSelector(".JCContentMiddle")).FindElement(By.TagName("a")).GetAttribute("href");

                //salvare in baza de date a joburilor
                anunturi anunt = new anunturi();
                anunt.a_jid = Convert.ToInt32((from j in db.jobs where j.j_name == citirejob select j.j_id).Single()); //atribuire id job pe baza numelui jobului introdus 
                anunt.a_firma = firma;
                anunt.a_post = post;
                anunt.a_locatie = locatie;
                anunt.a_salariu = salariu;
                anunt.a_platf = platforma;
                anunt.a_date = DateTime.Now;
                anunt.a_link = link;

                try
                {
                    //salvarea efectiva in baza de date prin SaveChanges
                    if (!db.anunturis.Any(c => c.a_link == anunt.a_link))
                    {
                        Console.WriteLine("Firma: " + firma + " Post: " + post + " Locatia: " + locatie + " Salariu: " + salariu + " Platforma: " + platforma + "\nLink: " + link);
                        db.anunturis.Add(anunt);
                        db.SaveChanges();
                    }
                }
                catch (DbEntityValidationException e)
                {
                    //eroare generata automat pentru a vedea exact ce anume nu functioneaza in baza de date
                    foreach (var eve in e.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                                                       eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                                                               ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                }

                anunt = null;
            }


        }


        //functie pentru generare pdf
        static void ExportPDF()
        {
            RecrutareEntities db = new RecrutareEntities();
            string caleFisier = "D:\\joburi.pdf"; //calea de salvare a fisierului pdf -- TREBUIE MODIFICATA IN FUNCTIE DE UNDE VRETI SA SALVATI FISIERUL
            List<anunturi> lista = db.anunturis.ToList();
            using (var fileStream = new FileStream(caleFisier, FileMode.Create))
            {
                using (var writer = new PdfWriter(fileStream))
                {
                    using (var pdf = new PdfDocument(writer))
                    {
                        var document = new Document(pdf);
                        document.Add(new Paragraph("PLOIESTI: "));
                        foreach (anunturi isb in lista)
                        {
                            document.Add(new Paragraph(isb.a_id + ". Job ID: " +isb.a_jid + " Firma: " + isb.a_firma + " Post: " + isb.a_post + " Locatia: " + isb.a_locatie + " Salariu: " + isb.a_salariu + " Platforma: " + isb.a_platf + " Data: " + isb.a_date + "\nLink: " + isb.a_link));
                        }
                    }
                }
            }
            Console.WriteLine("Fisierul pdf a fost generat cu succes la adresa: " + caleFisier);

        }
    }

}
