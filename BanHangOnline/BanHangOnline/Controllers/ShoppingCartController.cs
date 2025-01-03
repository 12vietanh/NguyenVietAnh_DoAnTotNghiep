﻿using BanHangOnline.Models;
using BanHangOnline.Models.EF;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using VNPAY_CS_ASPX;

namespace BanHangOnline.Controllers
{
    public class ShoppingCartController : Controller
    {
        ApplicationDbContext db = new ApplicationDbContext();
        // GET: ShoppingCart
        public ActionResult Index()
        {
            ShoppingCart cart = (ShoppingCart)Session["Cart"];
            if (cart != null)
            {
                return View(cart.Items);
            }
            return View();
        }

        public ActionResult CheckOut()
        {
            ShoppingCart cart = (ShoppingCart)Session["Cart"];
            if (cart != null)
            {
                return View(cart.Items);
            }
            return View();
        }
        
        public ActionResult RenderAddress(OverViewModel req, string customerName, string Address, int Phone)
        {
            if (ModelState.IsValid)
            {
                req.CustomerName = customerName;
                req.Phone = Convert.ToString( Phone);
                req.Address = Address;
                //return Json(code);
                return Json(new { Success = true, customerName = req.CustomerName, Phone = req.Phone, Address = req.Address });
            }
            return Json(new {Success = false});
        }

        public ActionResult VnpayReturn()
        {
            if (Request.QueryString.Count > 0)
            {
                string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"]; //Chuoi bi mat
                var vnpayData = Request.QueryString;
                VnPayLibrary vnpay = new VnPayLibrary();

                foreach (string s in vnpayData)
                {
                    //get all querystring data
                    if (!string.IsNullOrEmpty(s) && s.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(s, vnpayData[s]);
                    }
                }
                string orderCode = Convert.ToString(vnpay.GetResponseData("vnp_TxnRef"));
                long vnpayTranId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
                String vnp_SecureHash = Request.QueryString["vnp_SecureHash"];
                String TerminalID = Request.QueryString["vnp_TmnCode"];
                long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100;
                String bankCode = Request.QueryString["vnp_BankCode"];

                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);
                if (checkSignature)
                {
                    if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                    {

                        var itemOrder = db.Orders.FirstOrDefault(x => x.Code == orderCode);
                        if (itemOrder != null)
                        {
                            itemOrder.statusPayment = 1;
                            db.Orders.Attach(itemOrder);
                            db.Entry(itemOrder).State = System.Data.Entity.EntityState.Modified;
                            db.SaveChanges();
                        }
                        //Thanh toan thanh cong
                        ViewBag.code = 0;
                        ViewBag.orderCode = orderCode;
                        ViewBag.InnerText = "Giao dịch được thực hiện thành công. Cảm ơn quý khách đã sử dụng dịch vụ";
                        //log.InfoFormat("Thanh toan thanh cong, OrderId={0}, VNPAY TranId={1}", orderId, vnpayTranId);
                    }
                    else
                    {
                        var itemOrder = db.Orders.FirstOrDefault(x => x.Code == orderCode);
                        if (itemOrder != null)
                        {
                            db.Orders.Remove(itemOrder);
                            db.SaveChanges();
                        }
                        //Thanh toan khong thanh cong. Ma loi: vnp_ResponseCode
                        ViewBag.code = 1;
                        ViewBag.InnerText = "Có lỗi xảy ra trong quá trình xử lý.Mã lỗi: " + vnp_ResponseCode;
                        //log.InfoFormat("Thanh toan loi, OrderId={0}, VNPAY TranId={1},ResponseCode={2}", orderId, vnpayTranId, vnp_ResponseCode);
                    }
                    //displayTmnCode.InnerText = "Mã Website (Terminal ID):" + TerminalID;
                    //displayTxnRef.InnerText = "Mã giao dịch thanh toán:" + orderId.ToString();
                    //displayVnpayTranNo.InnerText = "Mã giao dịch tại VNPAY:" + vnpayTranId.ToString();
                    ViewBag.ThanhToanThanhCong = "Số tiền thanh toán (VND):" + vnp_Amount.ToString();
                    //displayBankCode.InnerText = "Ngân hàng thanh toán:" + bankCode;
                }
            }
            //var a = UrlPayment(0, "DH3574");
            return View();
        }

        [HttpPost]
        public ActionResult CheckOutCart(string customerNameBill, string AddressBill, string PhoneBill, string TypePayment, string Note, string IdCustomer, string TyPaymentVN)
        {
            var code = new { Success = false, code = "", Url = "", type = 0 };
            if (ModelState.IsValid)
            {
                ShoppingCart cart = (ShoppingCart)Session["Cart"];
                if (cart != null)
                {
                    Order order = new Order();
                    order.CustomerName = customerNameBill;
                    order.Phone = PhoneBill;
                    order.Address = AddressBill;
                    cart.Items.ForEach(x => order.OrderDetails.Add(new OrderDetail
                    {
                        ProductID = x.ProductId,
                        Quantity = x.Quantity,
                        Price = x.Price
                    }));
                    order.Quantity = cart.Items.Sum(x=>x.Quantity);
                    order.TotalAmount = cart.Items.Sum(x => (x.Price * x.Quantity));
                    order.TypePayment = TypePayment;
                    order.statusPayment = 0;
                    order.note = Note;
                    order.statusOrder = -1;
                    order.CreatedDate = DateTime.Now;
                    order.ModifiedDate = DateTime.Now;
                    order.CreatedBy = PhoneBill;
                    order.IdCustomer = IdCustomer;
                    Random rd = new Random();
                    order.Code = "HD" + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9) + rd.Next(0, 9);
                    db.Orders.Add(order);
                    db.SaveChanges();
                    cart.ClearCart();
                    if(TypePayment == "VNPAY")
                    {
                        var url = UrlPayment(Convert.ToInt32(TyPaymentVN), order.Code);
                        code = new { Success = true, code = order.Code, Url = url, type = 2 };
                    }
                    if(TypePayment == "COD")
                    {
                        code = new { Success = true, code = order.Code, Url = "", type = 1 };
                    }
                }
            }
            return Json(code);
        }

        public ActionResult ToOrderDetail(string code)
        {
            var orderDetail = db.Orders.Where(x=>x.Code == code).FirstOrDefault();
            return View(orderDetail);
        }

        public ActionResult ProductInOrderDetail(int Id)
        {
            var ProductOrderDetail = db.OrdersDetails.Where(x => x.OrderID == Id).ToList();
            //return View(ProductOrderDetail);
            return PartialView(ProductOrderDetail);
        }


        public ActionResult ShowCount()
        {
            ShoppingCart cart = (ShoppingCart)Session["Cart"];
            if(cart != null)
            {
                return Json(new { Success = true, count = cart.Items.Count}, JsonRequestBehavior.AllowGet);
            }
            return Json(new { Success = false, count = 0, }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult AddToCart(int id, int quantity)
        {
            var code = new { Success = false, msg = "", code = -1, count = 0 };
            var db = new ApplicationDbContext();
            var checkProduct = db.Products.FirstOrDefault(x => x.Id == id);
            if(checkProduct != null)
            {
                ShoppingCart cart =(ShoppingCart)Session["Cart"];
                if(cart == null)
                {
                    cart = new ShoppingCart();
                }
                ShoppingCartItem item = new ShoppingCartItem
                {
                    ProductId = checkProduct.Id,
                    ProductName = checkProduct.Title,
                    Alias = checkProduct.Alias,
                    Categoryname = checkProduct.ProductCategory.Title,
                    ProductCategoryID = Convert.ToString(checkProduct.ProductCategoryID),
                    Quantity = quantity,
                };
                if (checkProduct.ProductImages.FirstOrDefault(x => x.IsDefault) != null)
                {
                    item.ProductImage = checkProduct.ProductImages.FirstOrDefault().Image;
                }
                if (checkProduct.PriceSale > 0 && checkProduct.IsSale == true)
                {
                    item.Price = (decimal)checkProduct.PriceSale;
                }
                else
                {
                    item.Price = checkProduct.Price;
                }
                item.TotalPrice = item.Quantity * item.Price;
                cart.AddToCart(item, quantity);
                Session["Cart"] = cart;
                code = new { Success = true, msg = "Them thanh cong", code = 1, count = cart.Items.Count };
            }
            return Json(code);
        }

        [HttpPost]
        public ActionResult Delete(int Id)
        {
            ShoppingCart cart = (ShoppingCart)Session["Cart"];
            var checkProduct = cart.Items.FirstOrDefault(x=>x.ProductId == Id);
            if (checkProduct != null)
            {
                cart.Remove(Id);
                return Json(new { success = true, count = cart.Items.Count });
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public ActionResult Update(int Id, int Quantity)
        {
            ShoppingCart cart = (ShoppingCart)Session["Cart"];
            var checkProduct = cart.Items.FirstOrDefault(x => x.ProductId == Id);
            if (checkProduct != null && Quantity > 0)
            {
                cart.UpdateQuantity(Id, Quantity);
                return Json(new { success = true, quantity = Quantity });
            }

            if(checkProduct != null && Quantity <= 0)
            {
                cart.Remove(Id);
                return Json(new { success = true, quantity = Quantity });
            }

            return Json(new { success = false, quantity = Quantity });
        }

        [HttpPost]
        public ActionResult deleteAll(string ids)
        {
            ShoppingCart cart = (ShoppingCart)Session["Cart"];
            if (!string.IsNullOrEmpty(ids))
            {
                var items = ids.Split(',');
                if (items != null && items.Any())
                {
                    foreach (var item in items)
                    {
                        var Id = Convert.ToInt32(item);
                        var checkProduct = cart.Items.FirstOrDefault(x => x.ProductId == Id);
                        cart.Remove(Id);
                    }
                }
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        public string UrlPayment(int TypePaymentVN, string orderCode)
        {
            var urlPayment = "";
            var order = db.Orders.FirstOrDefault(x => x.Code == orderCode);
            //Get Config Info
            string vnp_Returnurl = ConfigurationManager.AppSettings["vnp_Returnurl"]; //URL nhan ket qua tra ve 
            string vnp_Url = ConfigurationManager.AppSettings["vnp_Url"]; //URL thanh toan cua VNPAY 
            string vnp_TmnCode = ConfigurationManager.AppSettings["vnp_TmnCode"]; //Ma định danh merchant kết nối (Terminal Id)
            string vnp_HashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"]; //Secret Key

            //Build URL for VNPAY
            VnPayLibrary vnpay = new VnPayLibrary();
            var Price = (long)order.TotalAmount * 100;
            vnpay.AddRequestData("vnp_Version", VnPayLibrary.VERSION);
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", Price.ToString()); //Số tiền thanh toán. Số tiền không mang các ký tự phân tách thập phân, phần nghìn, ký tự tiền tệ. Để gửi số tiền thanh toán là 100,000 VND (một trăm nghìn VNĐ) thì merchant cần nhân thêm 100 lần (khử phần thập phân), sau đó gửi sang VNPAY là: 10000000
            if (TypePaymentVN == 1)
            {
                vnpay.AddRequestData("vnp_BankCode", "VNPAYQR");
            }
            else if (TypePaymentVN == 2)
            {
                vnpay.AddRequestData("vnp_BankCode", "VNBANK");
            }
            else if (TypePaymentVN == 3)
            {
                vnpay.AddRequestData("vnp_BankCode", "INTCARD");
            }

            vnpay.AddRequestData("vnp_CreateDate", order.CreatedDate.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", Utils.GetIpAddress());
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toán đơn hàng :" + order.Code);
            vnpay.AddRequestData("vnp_OrderType", "other"); //default value: other

            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", order.Code); // Mã tham chiếu của giao dịch tại hệ thống của merchant. Mã này là duy nhất dùng để phân biệt các đơn hàng gửi sang VNPAY. Không được trùng lặp trong ngày

            //Add Params of 2.1.0 Version
            //Billing

            urlPayment = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
            //log.InfoFormat("VNPAY URL: {0}", paymentUrl);
            return urlPayment;
        }


    }
}