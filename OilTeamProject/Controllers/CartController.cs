using OilTeamProject.Models.Customers;
using OilTeamProject.Models.Products;
using OilTeamProject.Persistence;
using OilTeamProject.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace OilTeamProject.Controllers
{
    public class CartController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Cart
        public ActionResult Index()
        {
            // Init the cart list
            var cart = Session["cart"] as List<CartViewModel> ?? new List<CartViewModel>();

            // Check if cart is empty
            if (cart.Count == 0 || Session["cart"] == null)
            {
                ViewBag.Message = "Your cart is empty";
                return View();
            }

            // Calculate total and save to ViewBag
            double total = 0;
            foreach (var item in cart)
            {
                total += item.Total;
            }
            ViewBag.GrandTotal = total;

            // Return view with list
            return View(cart);
        }

        public ActionResult CartPartial()
        {
            // Init CartVM
            CartViewModel model = new CartViewModel();

            // Init quantity
            int qty = 0;

            // Init price
            double price = 0;

            // Init grandTotal
            double total = 0;

            // Check for cart session
            if (Session["cart"] != null)
            {
                // Get total qty and price
                var list = (List<CartViewModel>)Session["cart"];

                foreach (var item in list)
                {
                    qty += item.Quantity;
                    price = item.Price;
                    total += item.Total;
                }
                model.Quantity = qty;
                model.Price = price;
                model.GrandTotal = total;
            }
            else
            {
                // Or set qty and price to 0
                model.Quantity = 0;
                model.Price = 0;
            }

            // Return partial view with model
            return PartialView(model);
        }


        public ActionResult AddToCartPartial(int id)
        {
            // Init CartViewModel list
            List<CartViewModel> cart = Session["cart"] as List<CartViewModel> ?? new List<CartViewModel>();

            // Init CartViewModel
            CartViewModel model = new CartViewModel();

            // Get the product
            Product product = db.Products.Find(id);

            // Check if the product is already in cart
            var productInCart = cart.FirstOrDefault(x => x.ProductId == id);

            // If not, add new
            if (productInCart == null)
            {
                cart.Add(new CartViewModel()
                {
                    ProductId = product.ID,
                    ProductName = product.Name,
                    Quantity = 1,
                    Price = (product.DiscountedPrice == 0) ? product.Price : product.DiscountedPrice,
                    Image = product.Thumbnail
                });
            }
            else
            {
                // If it is, increment
                productInCart.Quantity++;
            }

            // Get total qty and price and add to model

            int qty = 0;
            double price = 0;
            double total = 0;

            foreach (var item in cart)
            {
                qty += item.Quantity;
                price += item.Quantity * item.Price;
                total += item.Total;
            }

            model.Quantity = qty;
            model.Price = price;
            model.GrandTotal = total;

            // Save cart back to session
            Session["cart"] = cart;

            // Return partial view with model
            return PartialView(model);
        }


        // GET: /Cart/IncrementProduct
        public JsonResult IncrementProduct(int productId)
        {
            // Init cart list
            List<CartViewModel> cart = Session["cart"] as List<CartViewModel>;

            // Get cartVM from list
            CartViewModel model = cart.FirstOrDefault(x => x.ProductId == productId);

            // Increment qty
            model.Quantity++;

            // Store needed data
            var result = new { qty = model.Quantity, price = model.Price };

            // Return json with data
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // GET: /Cart/DecrementProduct
        public ActionResult DecrementProduct(int productId)
        {
            // Init cart
            List<CartViewModel> cart = Session["cart"] as List<CartViewModel>;

            // Get model from list
            CartViewModel model = cart.FirstOrDefault(x => x.ProductId == productId);

            // Decrement qty
            if (model.Quantity > 1)
            {
                model.Quantity--;
            }
            else
            {
                model.Quantity = 0;
                cart.Remove(model);
            }

            // Store needed data
            var result = new { qty = model.Quantity, price = model.Price };

            // Return json
            return Json(result, JsonRequestBehavior.AllowGet);
        }

        // GET: /Cart/RemoveProduct
        public void RemoveProduct(int productId)
        {
            // Init cart list
            List<CartViewModel> cart = Session["cart"] as List<CartViewModel>;

            // Get model from list
            CartViewModel model = cart.FirstOrDefault(x => x.ProductId == productId);

            // Remove model from list
            cart.Remove(model);

        }


        public ActionResult PaypalPartial()
        {
            List<CartViewModel> cart = Session["cart"] as List<CartViewModel>;

            return PartialView(cart);
        }

        //POST: /Cart/PlaceOrder
        [HttpPost]
        public void PlaceOrder()
        {
            //Get cart list
            List<CartViewModel> cart = Session["cart"] as List<CartViewModel>;

            var customer = db.Customers
                      .OrderBy(c => Guid.NewGuid())
                      .FirstOrDefault();
            // ADD ORDER TO DATABASE
            var orderNumber = Order.GetOrderNumber(customer.CustomerID, db);
            var paidOf = true;
            double total = 0;
            var productQuantities = new List<int>();
            var productStocks = new List<string>();
            var productNames = new List<string>();
            var prices = new List<double>();
            var addToCarts = new List<bool>();
            var descriptions = new List<string>();
            var maxProductStock = new ProductStock();

            var minProductStock = new ProductStock().SendToCustomerView(2);
            foreach (var item in cart)
            {
                total += item.Total;
                prices.Add(item.Total);
                productQuantities.Add(item.Quantity);
                maxProductStock = maxProductStock.SendToCustomerView(item.ProductId);


                productStocks.Add(maxProductStock.ProductStockID);
                var maxProductStockproduct = db.Products
                    .Where(p => p.ID == maxProductStock.ProductID)
                    .SingleOrDefault();
                productNames.Add(maxProductStockproduct.Name);
                addToCarts.Add(true);
                descriptions.Add(maxProductStockproduct.Description);

            }
            var totalCost = total;
            var paymentType = PaymentType.Paypal;
            var orderStatus = OrderStatus.Fulfilled;
            var dateTime = DateTime.Now;

            var formViewModel = new OrderFormViewModel()
            {
                ProductStockIds = productStocks,
                ProductNames = productNames,
                Prices = prices,
                Quantities = productQuantities,
                AddToCarts = addToCarts,
                Descriptions = descriptions,
            };

            Order order = new Order(
                orderNumber,
                paidOf,
                totalCost,
                paymentType,
                customer,
                orderStatus,
                dateTime);

            var ViewModel = Order.FillOrderFormViewModel(db);
            ViewModel.CustomerId = customer.CustomerID;
            ViewModel.TotalCost = totalCost;
            ViewModel.PaymentType = paymentType;

            if (ModelState.IsValid)
            {
                db.Orders.Add(order);

                var orderProducts = new OrderProducts();
                for (var i = 0; i < productStocks.Count; i++)
                {
                    orderProducts.ProductStockID = productStocks[i];
                    orderProducts.OrderID = order.OrderID;
                    orderProducts.Quantity = productQuantities[i];
                    db.OrderProducts.Add(orderProducts);
                    db.SaveChanges();
                }
            }
            // Reset session
            Session["cart"] = null;
        }
    }
}