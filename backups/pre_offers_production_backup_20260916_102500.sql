--
-- PostgreSQL database dump
--

\restrict RG7vimMN95Xs7ktfpkMBdayrYPEfG5k2MwR0y6ORuAdIk2j9k8keM8iIvI32r8Y

-- Dumped from database version 17.11 (c4ba6b8)
-- Dumped by pg_dump version 18.6

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

ALTER TABLE IF EXISTS ONLY public.sales DROP CONSTRAINT IF EXISTS fk_sales_sales_day;
ALTER TABLE IF EXISTS ONLY public.sale_payments DROP CONSTRAINT IF EXISTS fk_sale_payments_sale;
ALTER TABLE IF EXISTS ONLY public.sale_payments DROP CONSTRAINT IF EXISTS fk_sale_payments_payment_method;
ALTER TABLE IF EXISTS ONLY public.sale_items DROP CONSTRAINT IF EXISTS fk_sale_items_sale;
ALTER TABLE IF EXISTS ONLY public.sale_items DROP CONSTRAINT IF EXISTS fk_sale_items_retail_price;
ALTER TABLE IF EXISTS ONLY public.sale_items DROP CONSTRAINT IF EXISTS fk_sale_items_product;
ALTER TABLE IF EXISTS ONLY public.product_retail_prices DROP CONSTRAINT IF EXISTS fk_product_retail_prices_product;
ALTER TABLE IF EXISTS ONLY public.orderitems DROP CONSTRAINT IF EXISTS fk_product_order;
ALTER TABLE IF EXISTS ONLY public.cartitems DROP CONSTRAINT IF EXISTS fk_product_cart;
ALTER TABLE IF EXISTS ONLY public.paymentmethods DROP CONSTRAINT IF EXISTS fk_paymentmethods_storesettings;
ALTER TABLE IF EXISTS ONLY public.orderitems DROP CONSTRAINT IF EXISTS fk_orderitems_retail_price;
ALTER TABLE IF EXISTS ONLY public.orderdetails DROP CONSTRAINT IF EXISTS fk_orderdetails_order;
ALTER TABLE IF EXISTS ONLY public.orderitems DROP CONSTRAINT IF EXISTS fk_order;
ALTER TABLE IF EXISTS ONLY public.products DROP CONSTRAINT IF EXISTS fk_category;
ALTER TABLE IF EXISTS ONLY public.cartitems DROP CONSTRAINT IF EXISTS fk_cartitems_retail_price;
ALTER TABLE IF EXISTS ONLY public.cartitems DROP CONSTRAINT IF EXISTS fk_cart;
ALTER TABLE IF EXISTS ONLY public.deliveryorders DROP CONSTRAINT IF EXISTS "FK_deliveryorders_orders_orderid";
DROP INDEX IF EXISTS public.ux_users_phone;
DROP INDEX IF EXISTS public.ux_users_email;
DROP INDEX IF EXISTS public.ux_sales_days_created_by_open;
DROP INDEX IF EXISTS public.ux_product_retail_prices_product_size;
DROP INDEX IF EXISTS public.ux_carts_userid;
DROP INDEX IF EXISTS public.ux_cartitems_cart_product_retail;
DROP INDEX IF EXISTS public.ix_site_state_event_receipts_site_revision;
DROP INDEX IF EXISTS public.ix_sales_sales_day_status;
DROP INDEX IF EXISTS public.ix_sales_invoice_number;
DROP INDEX IF EXISTS public.ix_sales_completed_effective_date;
DROP INDEX IF EXISTS public.ix_sale_payments_sale_id;
DROP INDEX IF EXISTS public.ix_sale_payments_payment_method_id;
DROP INDEX IF EXISTS public.ix_sale_items_sale_id;
DROP INDEX IF EXISTS public.ix_sale_items_product_id;
DROP INDEX IF EXISTS public.ix_products_total_sold;
DROP INDEX IF EXISTS public.ix_products_name_trgm;
DROP INDEX IF EXISTS public.ix_products_description_trgm;
DROP INDEX IF EXISTS public.ix_products_createdat;
DROP INDEX IF EXISTS public.ix_products_categoryid_createdat;
DROP INDEX IF EXISTS public.ix_products_brand_trgm;
DROP INDEX IF EXISTS public.ix_product_retail_prices_active_size;
DROP INDEX IF EXISTS public.ix_paymentmethods_storesettingsid;
DROP INDEX IF EXISTS public.ix_orders_status_orderdate;
DROP INDEX IF EXISTS public.ix_orders_orderdate;
DROP INDEX IF EXISTS public.ix_orders_cancelledat;
DROP INDEX IF EXISTS public.ix_catalog_outbox_events_status_next_attempt_at;
DROP INDEX IF EXISTS public.ix_catalog_outbox_events_entity;
DROP INDEX IF EXISTS public."IX_securitylogs_userid";
DROP INDEX IF EXISTS public."IX_sale_items_retail_price_id";
DROP INDEX IF EXISTS public."IX_orders_userid";
DROP INDEX IF EXISTS public."IX_orderitems_retail_price_id";
DROP INDEX IF EXISTS public."IX_orderdetails_orderid";
DROP INDEX IF EXISTS public."IX_cartitems_retail_price_id";
ALTER TABLE IF EXISTS ONLY public.visits DROP CONSTRAINT IF EXISTS visits_pkey;
ALTER TABLE IF EXISTS ONLY public.users DROP CONSTRAINT IF EXISTS users_pkey;
ALTER TABLE IF EXISTS ONLY public.storesettings DROP CONSTRAINT IF EXISTS storesettings_pkey;
ALTER TABLE IF EXISTS ONLY public.site_state_sync_checkpoints DROP CONSTRAINT IF EXISTS site_state_sync_checkpoints_pkey;
ALTER TABLE IF EXISTS ONLY public.site_state_event_receipts DROP CONSTRAINT IF EXISTS site_state_event_receipts_pkey;
ALTER TABLE IF EXISTS ONLY public.securitylogs DROP CONSTRAINT IF EXISTS securitylogs_pkey;
ALTER TABLE IF EXISTS ONLY public.sales DROP CONSTRAINT IF EXISTS sales_pkey;
ALTER TABLE IF EXISTS ONLY public.sales_days DROP CONSTRAINT IF EXISTS sales_days_pkey;
ALTER TABLE IF EXISTS ONLY public.sale_payments DROP CONSTRAINT IF EXISTS sale_payments_pkey;
ALTER TABLE IF EXISTS ONLY public.sale_items DROP CONSTRAINT IF EXISTS sale_items_pkey;
ALTER TABLE IF EXISTS ONLY public.products DROP CONSTRAINT IF EXISTS products_pkey;
ALTER TABLE IF EXISTS ONLY public.product_retail_prices DROP CONSTRAINT IF EXISTS product_retail_prices_pkey;
ALTER TABLE IF EXISTS ONLY public.paymentmethods DROP CONSTRAINT IF EXISTS paymentmethods_pkey;
ALTER TABLE IF EXISTS ONLY public.orders DROP CONSTRAINT IF EXISTS orders_pkey;
ALTER TABLE IF EXISTS ONLY public.orderitems DROP CONSTRAINT IF EXISTS orderitems_pkey;
ALTER TABLE IF EXISTS ONLY public.orderdetails DROP CONSTRAINT IF EXISTS orderdetails_pkey;
ALTER TABLE IF EXISTS ONLY public.local_site_state_snapshots DROP CONSTRAINT IF EXISTS local_site_state_snapshots_pkey;
ALTER TABLE IF EXISTS ONLY public.deliveryorders DROP CONSTRAINT IF EXISTS deliveryorders_pkey;
ALTER TABLE IF EXISTS ONLY public.categories DROP CONSTRAINT IF EXISTS categories_pkey;
ALTER TABLE IF EXISTS ONLY public.catalog_outbox_events DROP CONSTRAINT IF EXISTS catalog_outbox_events_pkey;
ALTER TABLE IF EXISTS ONLY public.carts DROP CONSTRAINT IF EXISTS carts_pkey;
ALTER TABLE IF EXISTS ONLY public.cartitems DROP CONSTRAINT IF EXISTS cartitems_pkey;
ALTER TABLE IF EXISTS ONLY public."UserSite" DROP CONSTRAINT IF EXISTS "UserSite_pkey";
ALTER TABLE IF EXISTS ONLY public."__EFMigrationsHistory" DROP CONSTRAINT IF EXISTS "PK___EFMigrationsHistory";
ALTER TABLE IF EXISTS ONLY public."DataProtectionKeys" DROP CONSTRAINT IF EXISTS "PK_DataProtectionKeys";
ALTER TABLE IF EXISTS ONLY public."UserSite" DROP CONSTRAINT IF EXISTS "AK_UserSite_UserID";
ALTER TABLE IF EXISTS public.visits ALTER COLUMN id DROP DEFAULT;
ALTER TABLE IF EXISTS public.users ALTER COLUMN id DROP DEFAULT;
ALTER TABLE IF EXISTS public.storesettings ALTER COLUMN id DROP DEFAULT;
ALTER TABLE IF EXISTS public.securitylogs ALTER COLUMN id DROP DEFAULT;
ALTER TABLE IF EXISTS public.paymentmethods ALTER COLUMN id DROP DEFAULT;
ALTER TABLE IF EXISTS public.deliveryorders ALTER COLUMN orderid DROP DEFAULT;
DROP SEQUENCE IF EXISTS public.visits_id_seq;
DROP TABLE IF EXISTS public.visits;
DROP SEQUENCE IF EXISTS public.users_id_seq;
DROP TABLE IF EXISTS public.users;
DROP SEQUENCE IF EXISTS public.storesettings_id_seq;
DROP TABLE IF EXISTS public.storesettings;
DROP TABLE IF EXISTS public.site_state_sync_checkpoints;
DROP TABLE IF EXISTS public.site_state_event_receipts;
DROP SEQUENCE IF EXISTS public.securitylogs_id_seq;
DROP TABLE IF EXISTS public.securitylogs;
DROP TABLE IF EXISTS public.sales_days;
DROP TABLE IF EXISTS public.sales;
DROP TABLE IF EXISTS public.sale_payments;
DROP TABLE IF EXISTS public.sale_items;
DROP SEQUENCE IF EXISTS public.products_id_seq;
DROP TABLE IF EXISTS public.products;
DROP TABLE IF EXISTS public.product_retail_prices;
DROP SEQUENCE IF EXISTS public.paymentmethods_id_seq;
DROP TABLE IF EXISTS public.paymentmethods;
DROP SEQUENCE IF EXISTS public.orders_id_seq;
DROP TABLE IF EXISTS public.orders;
DROP SEQUENCE IF EXISTS public.orderitems_id_seq;
DROP TABLE IF EXISTS public.orderitems;
DROP TABLE IF EXISTS public.orderdetails;
DROP TABLE IF EXISTS public.local_site_state_snapshots;
DROP SEQUENCE IF EXISTS public.deliveryorders_orderid_seq;
DROP TABLE IF EXISTS public.deliveryorders;
DROP SEQUENCE IF EXISTS public.categories_id_seq;
DROP TABLE IF EXISTS public.categories;
DROP TABLE IF EXISTS public.catalog_outbox_events;
DROP SEQUENCE IF EXISTS public.carts_id_seq;
DROP TABLE IF EXISTS public.carts;
DROP SEQUENCE IF EXISTS public.cartitems_id_seq;
DROP TABLE IF EXISTS public.cartitems;
DROP TABLE IF EXISTS public."__EFMigrationsHistory";
DROP TABLE IF EXISTS public."UserSite";
DROP TABLE IF EXISTS public."DataProtectionKeys";
DROP EXTENSION IF EXISTS pg_trgm;
--
-- Name: pg_trgm; Type: EXTENSION; Schema: -; Owner: -
--

CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;


--
-- Name: EXTENSION pg_trgm; Type: COMMENT; Schema: -; Owner: -
--

COMMENT ON EXTENSION pg_trgm IS 'text similarity measurement and index searching based on trigrams';


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: DataProtectionKeys; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DataProtectionKeys" (
    "Id" integer NOT NULL,
    "FriendlyName" text,
    "Xml" text
);


--
-- Name: DataProtectionKeys_Id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."DataProtectionKeys" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public."DataProtectionKeys_Id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: UserSite; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UserSite" (
    id integer NOT NULL,
    "UserID" integer DEFAULT 0 NOT NULL,
    "Role" character varying DEFAULT 'Customer'::character varying,
    "SearchName" text,
    "SearchNameSyncVersion" bigint DEFAULT 0 NOT NULL
);


--
-- Name: UserSite_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public."UserSite" ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public."UserSite_id_seq"
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Name: cartitems; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.cartitems (
    id integer NOT NULL,
    cartid integer NOT NULL,
    productid integer NOT NULL,
    quantity integer DEFAULT 1 NOT NULL,
    retail_price_id integer,
    CONSTRAINT ck_cartitems_quantity_nonnegative CHECK ((quantity >= 0))
);


--
-- Name: cartitems_id_identity_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.cartitems ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.cartitems_id_identity_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: cartitems_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.cartitems_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: carts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.carts (
    id integer NOT NULL,
    userid integer NOT NULL,
    createdat timestamp without time zone DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text)
);


--
-- Name: carts_id_identity_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.carts ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.carts_id_identity_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: carts_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.carts_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: catalog_outbox_events; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.catalog_outbox_events (
    id bigint NOT NULL,
    entity_type character varying(50) NOT NULL,
    entity_id integer NOT NULL,
    operation_type character varying(20) NOT NULL,
    payload text,
    created_at timestamp without time zone NOT NULL,
    processed_at timestamp without time zone,
    attempt_count integer DEFAULT 0 NOT NULL,
    last_attempt_at timestamp without time zone,
    next_attempt_at timestamp without time zone,
    error_message text,
    status character varying(20) DEFAULT 'Pending'::character varying NOT NULL
);


--
-- Name: catalog_outbox_events_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.catalog_outbox_events ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.catalog_outbox_events_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: categories; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.categories (
    id integer NOT NULL,
    name character varying(100) NOT NULL,
    description text,
    imageurl text
);


--
-- Name: categories_id_identity_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.categories ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.categories_id_identity_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: categories_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.categories_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: deliveryorders; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.deliveryorders (
    orderid integer NOT NULL,
    fullname character varying(150) NOT NULL,
    phonenumber character varying(20) NOT NULL,
    secondphonenumber character varying(20),
    governorate character varying(100) NOT NULL,
    city character varying(100) NOT NULL,
    district character varying(100) NOT NULL
);


--
-- Name: deliveryorders_orderid_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.deliveryorders_orderid_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: deliveryorders_orderid_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.deliveryorders_orderid_seq OWNED BY public.deliveryorders.orderid;


--
-- Name: local_site_state_snapshots; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.local_site_state_snapshots (
    siteid integer NOT NULL,
    contractversion integer NOT NULL,
    mode character varying(20) NOT NULL,
    revision bigint NOT NULL,
    effectiveatutc timestamp with time zone NOT NULL,
    expiresatutc timestamp with time zone NOT NULL,
    sitename character varying(200) NOT NULL,
    siteurl text NOT NULL,
    startdate date NOT NULL,
    originaldurationdays integer NOT NULL,
    CONSTRAINT ck_local_site_state_contract_version CHECK ((contractversion = 1)),
    CONSTRAINT ck_local_site_state_duration CHECK ((originaldurationdays > 0)),
    CONSTRAINT ck_local_site_state_mode CHECK (((mode)::text = ANY ((ARRAY['Online'::character varying, 'Development'::character varying, 'Offline'::character varying])::text[]))),
    CONSTRAINT ck_local_site_state_revision CHECK ((revision >= 1)),
    CONSTRAINT ck_local_site_state_site_id CHECK ((siteid = 1))
);


--
-- Name: orderdetails; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.orderdetails (
    id integer NOT NULL,
    orderid integer NOT NULL,
    recipientname character varying(150),
    recipientphone character varying(50),
    governorate character varying(100),
    region character varying(100),
    district character varying(100),
    fulladdress text,
    paymentmethod character varying(100),
    accountname character varying(150),
    accountnumber character varying(100),
    transferreferencenumber character varying(100),
    paymentimagepath text,
    paymentnotes text,
    createdat timestamp without time zone,
    updatedat timestamp without time zone
);


--
-- Name: orderdetails_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.orderdetails ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.orderdetails_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: orderitems; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.orderitems (
    id integer NOT NULL,
    orderid integer NOT NULL,
    productid integer NOT NULL,
    quantity integer NOT NULL,
    unitprice numeric(10,2) NOT NULL,
    retail_price_id integer,
    retail_size_ml integer,
    fulfilled_quantity integer,
    unavailable_quantity integer,
    CONSTRAINT ck_orderitems_fulfilled_quantity_range CHECK (((fulfilled_quantity IS NULL) OR ((fulfilled_quantity >= 0) AND (fulfilled_quantity <= quantity)))),
    CONSTRAINT ck_orderitems_quantity_positive CHECK ((quantity > 0)),
    CONSTRAINT ck_orderitems_unavailable_quantity_range CHECK (((unavailable_quantity IS NULL) OR ((unavailable_quantity >= 0) AND (unavailable_quantity <= quantity)))),
    CONSTRAINT ck_orderitems_unitprice_nonnegative CHECK ((unitprice >= (0)::numeric))
);


--
-- Name: orderitems_id_identity_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.orderitems ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.orderitems_id_identity_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: orderitems_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.orderitems_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: orders; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.orders (
    id integer NOT NULL,
    userid integer NOT NULL,
    orderdate timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    totalamount numeric(10,2) NOT NULL,
    status character varying(50) NOT NULL,
    trackingnumber character varying(100),
    "TimeState" timestamp without time zone DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text),
    notes character varying(500),
    paymentmethod character varying(50),
    paymentstatus character varying(50) DEFAULT 'Unpaid'::character varying NOT NULL,
    receipturl text,
    stockdeducted boolean DEFAULT false NOT NULL,
    finalfulfilledamount numeric(10,2),
    paymentverifiedat timestamp without time zone,
    paymentverifiedbyuserid integer,
    workflowstate character varying(50),
    cancelledat timestamp without time zone,
    paymentreviewedat timestamp without time zone,
    paymentreviewedbyuserid integer,
    admin_note character varying(500),
    CONSTRAINT ck_orders_dh03_cancelledat CHECK ((((status)::text = 'Cancelled'::text) = (cancelledat IS NOT NULL))),
    CONSTRAINT ck_orders_dh03_conflict CHECK ((((workflowstate)::text <> 'ConflictAwaitingDecision'::text) OR (((status)::text = 'Pending'::text) AND (NOT stockdeducted) AND ((paymentstatus)::text <> 'Paid'::text) AND (paymentverifiedat IS NULL) AND (paymentverifiedbyuserid IS NULL) AND (finalfulfilledamount IS NULL)))),
    CONSTRAINT ck_orders_dh03_paid CHECK ((((status)::text <> 'Paid'::text) OR (stockdeducted AND ((paymentstatus)::text = 'Paid'::text) AND (paymentverifiedat IS NOT NULL) AND (paymentverifiedbyuserid IS NOT NULL) AND (paymentreviewedat IS NOT NULL) AND (paymentreviewedbyuserid = paymentverifiedbyuserid) AND (finalfulfilledamount IS NOT NULL)))),
    CONSTRAINT ck_orders_dh03_status CHECK (((status)::text = ANY ((ARRAY['Pending'::character varying, 'Paid'::character varying, 'Processed'::character varying, 'Shipped'::character varying, 'Delivered'::character varying, 'Cancelled'::character varying, 'Refunded'::character varying])::text[]))),
    CONSTRAINT ck_orders_dh03_stock_owner CHECK (((NOT stockdeducted) OR ((status)::text = ANY ((ARRAY['Paid'::character varying, 'Processed'::character varying, 'Shipped'::character varying, 'Delivered'::character varying])::text[])))),
    CONSTRAINT ck_orders_finalfulfilledamount_range CHECK (((finalfulfilledamount IS NULL) OR ((finalfulfilledamount >= (0)::numeric) AND (finalfulfilledamount <= totalamount)))),
    CONSTRAINT ck_orders_payment_review_pair CHECK (((paymentreviewedat IS NULL) = (paymentreviewedbyuserid IS NULL))),
    CONSTRAINT ck_orders_payment_verification_pair CHECK (((paymentverifiedat IS NULL) = (paymentverifiedbyuserid IS NULL))),
    CONSTRAINT ck_orders_totalamount_nonnegative CHECK ((totalamount >= (0)::numeric)),
    CONSTRAINT ck_orders_workflowstate CHECK (((workflowstate IS NULL) OR ((workflowstate)::text = ANY ((ARRAY['ConflictAwaitingDecision'::character varying, 'ConflictResolvedContinue'::character varying, 'ConflictResolvedCancel'::character varying])::text[]))))
);


--
-- Name: orders_id_identity_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.orders ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.orders_id_identity_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: orders_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.orders_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: paymentmethods; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.paymentmethods (
    id integer NOT NULL,
    type character varying(50) NOT NULL,
    name character varying(200) NOT NULL,
    accountholdername character varying(200) NOT NULL,
    accountnumber character varying(200) NOT NULL,
    instructions text,
    cardcolor character varying(20),
    isactive boolean DEFAULT true NOT NULL,
    storesettingsid integer DEFAULT 1 NOT NULL
);


--
-- Name: paymentmethods_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.paymentmethods_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: paymentmethods_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.paymentmethods_id_seq OWNED BY public.paymentmethods.id;


--
-- Name: product_retail_prices; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.product_retail_prices (
    id integer NOT NULL,
    product_id integer NOT NULL,
    size_ml integer NOT NULL,
    price numeric(10,2) NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    CONSTRAINT ck_product_retail_prices_price_positive CHECK ((price > (0)::numeric)),
    CONSTRAINT ck_product_retail_prices_size_positive CHECK ((size_ml > 0))
);


--
-- Name: product_retail_prices_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.product_retail_prices ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.product_retail_prices_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: products; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.products (
    id integer NOT NULL,
    categoryid integer NOT NULL,
    name character varying(255) NOT NULL,
    description text,
    price numeric(10,2) NOT NULL,
    stockquantity integer NOT NULL,
    imageurl text,
    createdat timestamp without time zone DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text),
    brand character varying(150),
    is_retail_enabled boolean DEFAULT false NOT NULL,
    stock_unit character varying(10) DEFAULT 'Piece'::character varying NOT NULL,
    volume_ml integer,
    total_sold integer DEFAULT 0 NOT NULL,
    sales_last_updated_at timestamp without time zone,
    CONSTRAINT ck_products_price_nonnegative CHECK ((price >= (0)::numeric)),
    CONSTRAINT ck_products_retail_requires_ml CHECK (((is_retail_enabled = false) OR (((stock_unit)::text = 'Ml'::text) AND (volume_ml IS NOT NULL) AND (volume_ml > 0)))),
    CONSTRAINT ck_products_stock_unit CHECK (((stock_unit)::text = ANY ((ARRAY['Piece'::character varying, 'Ml'::character varying])::text[]))),
    CONSTRAINT ck_products_stockquantity_nonnegative CHECK ((stockquantity >= 0)),
    CONSTRAINT ck_products_volume_for_ml CHECK (((((stock_unit)::text = 'Piece'::text) AND (volume_ml IS NULL)) OR (((stock_unit)::text = 'Ml'::text) AND (volume_ml IS NOT NULL) AND (volume_ml > 0))))
);


--
-- Name: products_id_identity_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.products ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.products_id_identity_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: products_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.products_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: sale_items; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sale_items (
    id integer NOT NULL,
    sale_id integer NOT NULL,
    product_id integer NOT NULL,
    product_name character varying(255),
    quantity integer DEFAULT 1 NOT NULL,
    unit_price numeric(18,2) DEFAULT 0.0 NOT NULL,
    discount numeric(18,2) DEFAULT 0.0 NOT NULL,
    total numeric(18,2) DEFAULT 0.0 NOT NULL,
    retail_price_id integer,
    retail_size_ml integer,
    CONSTRAINT "CK_SaleItem_Quantity_Positive" CHECK ((quantity > 0)),
    CONSTRAINT ck_sale_items_amounts_nonnegative CHECK (((unit_price >= (0)::numeric) AND (discount >= (0)::numeric) AND (total >= (0)::numeric)))
);


--
-- Name: sale_items_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.sale_items ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.sale_items_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: sale_payments; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sale_payments (
    id integer NOT NULL,
    sale_id integer NOT NULL,
    payment_method_id integer NOT NULL,
    amount numeric(18,2) DEFAULT 0.0 NOT NULL,
    transaction_reference character varying(150),
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text) NOT NULL,
    CONSTRAINT "CK_SalePayment_Amount_Positive" CHECK ((amount > (0)::numeric))
);


--
-- Name: sale_payments_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.sale_payments ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.sale_payments_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: sales; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sales (
    id integer NOT NULL,
    sales_day_id integer NOT NULL,
    invoice_number character varying(100),
    customer_name character varying(150),
    customer_phone character varying(50),
    total_amount numeric(18,2) DEFAULT 0.0 NOT NULL,
    discount_total numeric(18,2) DEFAULT 0.0 NOT NULL,
    final_amount numeric(18,2) DEFAULT 0.0 NOT NULL,
    status character varying(50) DEFAULT 'Draft'::character varying NOT NULL,
    notes text,
    created_by character varying(150),
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text) NOT NULL,
    completed_at timestamp without time zone,
    updated_at timestamp without time zone,
    draft_revision bigint DEFAULT 0 NOT NULL,
    edit_lock_expires_at timestamp with time zone,
    edit_locked_by character varying(256),
    edit_session_id uuid,
    CONSTRAINT ck_sales_amounts_nonnegative CHECK (((total_amount >= (0)::numeric) AND (discount_total >= (0)::numeric) AND (final_amount >= (0)::numeric)))
);


--
-- Name: sales_days; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.sales_days (
    id integer NOT NULL,
    date timestamp without time zone NOT NULL,
    status character varying(50) DEFAULT 'Open'::character varying NOT NULL,
    opening_balance numeric(18,2) DEFAULT 0.0 NOT NULL,
    total_sales numeric(18,2) DEFAULT 0.0 NOT NULL,
    total_cash numeric(18,2) DEFAULT 0.0 NOT NULL,
    total_transfer numeric(18,2) DEFAULT 0.0 NOT NULL,
    total_wallet numeric(18,2) DEFAULT 0.0 NOT NULL,
    total_returns numeric(18,2) DEFAULT 0.0 NOT NULL,
    net_total numeric(18,2) DEFAULT 0.0 NOT NULL,
    notes text,
    created_by character varying(150),
    closed_by character varying(150),
    created_at timestamp without time zone DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text) NOT NULL,
    closed_at timestamp without time zone,
    CONSTRAINT ck_sales_days_opening_balance_nonnegative CHECK ((opening_balance >= (0)::numeric)),
    CONSTRAINT ck_sales_days_totals_nonnegative CHECK (((total_sales >= (0)::numeric) AND (total_cash >= (0)::numeric) AND (total_transfer >= (0)::numeric) AND (total_wallet >= (0)::numeric) AND (total_returns >= (0)::numeric) AND (net_total >= (0)::numeric)))
);


--
-- Name: sales_days_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.sales_days ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.sales_days_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: sales_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

ALTER TABLE public.sales ALTER COLUMN id ADD GENERATED BY DEFAULT AS IDENTITY (
    SEQUENCE NAME public.sales_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: securitylogs; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.securitylogs (
    id integer NOT NULL,
    userid integer,
    action character varying(255) NOT NULL,
    ipaddress character varying(45) NOT NULL,
    deviceinfo text,
    riskscore integer DEFAULT 0,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP
);


--
-- Name: securitylogs_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.securitylogs_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: securitylogs_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.securitylogs_id_seq OWNED BY public.securitylogs.id;


--
-- Name: site_state_event_receipts; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.site_state_event_receipts (
    deliveryid uuid NOT NULL,
    siteid integer NOT NULL,
    revision bigint NOT NULL,
    payloadsha256 bytea NOT NULL,
    decision character varying(20) NOT NULL,
    recordedatutc timestamp with time zone NOT NULL,
    CONSTRAINT ck_site_state_event_receipts_decision CHECK (((decision)::text = ANY ((ARRAY['Applied'::character varying, 'Equal'::character varying, 'EqualConflict'::character varying, 'Stale'::character varying])::text[]))),
    CONSTRAINT ck_site_state_event_receipts_hash_length CHECK ((octet_length(payloadsha256) = 32)),
    CONSTRAINT ck_site_state_event_receipts_revision CHECK ((revision >= 1)),
    CONSTRAINT ck_site_state_event_receipts_site_id CHECK ((siteid = 1))
);


--
-- Name: site_state_sync_checkpoints; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.site_state_sync_checkpoints (
    siteid integer NOT NULL,
    lastattemptatutc timestamp with time zone NOT NULL,
    lastsuccessatutc timestamp with time zone,
    lastobservedremoterevision bigint,
    consecutivefailures integer NOT NULL,
    lastfailurecode character varying(64),
    updatedatutc timestamp with time zone NOT NULL,
    CONSTRAINT ck_site_state_sync_checkpoints_failures CHECK ((consecutivefailures >= 0)),
    CONSTRAINT ck_site_state_sync_checkpoints_remote_revision CHECK (((lastobservedremoterevision IS NULL) OR (lastobservedremoterevision >= 1))),
    CONSTRAINT ck_site_state_sync_checkpoints_site_id CHECK ((siteid = 1))
);


--
-- Name: storesettings; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.storesettings (
    id integer NOT NULL,
    whatsappnumber text DEFAULT ''::text,
    contactemail text DEFAULT ''::text,
    instagramlink text DEFAULT ''::text,
    twitterlink text DEFAULT ''::text,
    tiktoklink text DEFAULT ''::text,
    brandsmarquee text DEFAULT ''::text,
    featuredcategoryid integer,
    heromarketingtext text DEFAULT ''::text,
    heromarketingdesc text DEFAULT ''::text
);


--
-- Name: storesettings_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.storesettings_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: storesettings_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.storesettings_id_seq OWNED BY public.storesettings.id;


--
-- Name: users; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.users (
    id integer NOT NULL,
    name character varying(255) NOT NULL,
    phone text NOT NULL,
    passwordhash text NOT NULL,
    role character varying(50) DEFAULT 'Customer'::character varying NOT NULL,
    createdat timestamp without time zone DEFAULT CURRENT_TIMESTAMP,
    email character varying(260)
);


--
-- Name: users_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.users_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: users_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.users_id_seq OWNED BY public.users.id;


--
-- Name: visits; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public.visits (
    id bigint NOT NULL,
    visitdate timestamp without time zone DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'::text) NOT NULL,
    visitorname character varying(100),
    country character varying(100),
    governorate character varying(100),
    city character varying(100),
    device character varying(20),
    browser character varying(30)
);


--
-- Name: visits_id_seq; Type: SEQUENCE; Schema: public; Owner: -
--

CREATE SEQUENCE public.visits_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: visits_id_seq; Type: SEQUENCE OWNED BY; Schema: public; Owner: -
--

ALTER SEQUENCE public.visits_id_seq OWNED BY public.visits.id;


--
-- Name: deliveryorders orderid; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.deliveryorders ALTER COLUMN orderid SET DEFAULT nextval('public.deliveryorders_orderid_seq'::regclass);


--
-- Name: paymentmethods id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.paymentmethods ALTER COLUMN id SET DEFAULT nextval('public.paymentmethods_id_seq'::regclass);


--
-- Name: securitylogs id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.securitylogs ALTER COLUMN id SET DEFAULT nextval('public.securitylogs_id_seq'::regclass);


--
-- Name: storesettings id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.storesettings ALTER COLUMN id SET DEFAULT nextval('public.storesettings_id_seq'::regclass);


--
-- Name: users id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users ALTER COLUMN id SET DEFAULT nextval('public.users_id_seq'::regclass);


--
-- Name: visits id; Type: DEFAULT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.visits ALTER COLUMN id SET DEFAULT nextval('public.visits_id_seq'::regclass);


--
-- Data for Name: DataProtectionKeys; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."DataProtectionKeys" ("Id", "FriendlyName", "Xml") FROM stdin;
1	key-da346c65-2966-42fc-aa8a-054e5720222f	<key id="da346c65-2966-42fc-aa8a-054e5720222f" version="1"><creationDate>2026-08-16T08:30:01.334641Z</creationDate><activationDate>2026-08-16T08:30:01.334641Z</activationDate><expirationDate>2026-11-14T08:30:01.334641Z</expirationDate><descriptor deserializerType="Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel.AuthenticatedEncryptorDescriptorDeserializer, Microsoft.AspNetCore.DataProtection, Version=9.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60"><descriptor><encryption algorithm="AES_256_CBC" /><validation algorithm="HMACSHA256" /><masterKey p4:requiresEncryption="true" xmlns:p4="http://schemas.asp.net/2015/03/dataProtection"><!-- Warning: the key below is in an unencrypted form. --><value>y/9pTyyKqbnFxZgrsRPxIJjunfAGdiexAfdce+sulHCXfsLBfQUn885CD8HdPVko319jszzyPBLZCAcxF6MEng==</value></masterKey></descriptor></descriptor></key>
\.


--
-- Data for Name: UserSite; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."UserSite" (id, "UserID", "Role", "SearchName", "SearchNameSyncVersion") FROM stdin;
1	1	Developer	\N	0
6	6	Customer	\N	0
7	8	Customer	\N	0
9	10	Customer	\N	0
5	7	Customer	\N	0
11	5	Admin	\N	0
12	12	Customer	\N	0
13	33	Customer	\N	0
14	32	Customer	\N	0
15	29	Customer	\N	0
16	35	Customer	\N	0
17	34	Customer	\N	0
3	3	Admin	\N	0
4	4	Admin	\N	0
18	13	Customer	\N	0
19	14	Customer	\N	0
8	9	Customer	\N	0
10	11	Customer	\N	0
2	2	Customer	\N	0
20	15	Customer	\N	0
21	16	Customer	\N	0
\.


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20260728100505_AddPaymentMethodsAndStoreSettings	9.0.0
20260728102108_RemoveShippingColumnsFromStoreSettings	9.0.0
20260729073816_AddOrderDetailsTable	9.0.0
20260731221007_BaselineAndUpdates2	9.0.0
20260802192155_StoreSettingsDatabaseSync	9.0.0
20260803105052_UpdateModels	9.0.0
20260803210356_SyncDatabaseChanges	9.0.0
20260808154038_Group3StructuralIntegrity	9.0.0
20260808155546_Group3IdentityColumns	9.0.0
20260808160820_OrderStockDeductionState	9.0.0
20260808184947_Group4CartStability	9.0.0
20260731220152_BaselineAndUpdates	9.0.0
20260804090000_RestoreStorefrontSettingsColumns	9.0.0
20260809164627_AddQuickSalesSystem	9.0.0
20260810081715_UpdateModels_Anas	9.0.0
20260810212637_AddRetailSalesSupport	9.0.0
20260813192933_SyncModelChanges	9.0.0
20260816082757_AddDataProtectionKeys	9.0.0
20260816135606_AddStockAndPriceCheckConstraints	9.0.0
20260817072334_AddPartialUniqueIndexToSalesDays	9.0.0
20260818225847_AddPosDraftConcurrency	9.0.0
20260825072413_UpdateUserSiteIdentityAndRole	9.0.0
20260826061646_NormalizeEventTimeDefaults	9.0.0
20260826083642_DH03InventoryConflictWorkflow	9.0.0
20260826170129_AddSearchNameToUserSite	9.0.0
20260828124144_DH03CorrectOrderWorkflow	9.0.0
20260828195019_DH03CustomerConflictOwnership	9.0.0
20260901070000_AddProductBestSellerTracking	9.0.0
20260901122213_AddAdminNoteToOrders	9.0.0
20260903164333_Phase1StoreDatabaseIndexes	9.0.0
20260903205328_AddCatalogOutboxEvents	9.0.0
20260907071920_YAGOT01LocalStateFoundation	9.0.0
20260907180257_YAGOT03SiteStateSyncCheckpoint	9.0.0
20260909054954_Phase7StoreDatabaseOptimizations	9.0.0
\.


--
-- Data for Name: cartitems; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.cartitems (id, cartid, productid, quantity, retail_price_id) FROM stdin;
3	3	22	1	\N
278	7	38	1	\N
15	10	24	2	\N
\.


--
-- Data for Name: carts; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.carts (id, userid, createdat) FROM stdin;
1	34	2026-07-10 10:35:58.559496
2	33	2026-07-10 15:04:22.061294
3	29	2026-07-10 17:46:17.266425
4	35	2026-07-10 19:52:53.069198
5	32	2026-07-12 20:26:49.614366
6	8	2026-07-17 14:58:41.91281
7	2	2026-07-21 22:23:34.954021
8	3	2026-07-22 17:14:09.274823
9	1	2026-07-23 04:07:27.069161
10	4	2026-07-25 16:58:17.400573
11	7	2026-07-31 15:50:58.231695
12	9	2026-08-06 15:17:48.387647
13	10	2026-08-07 17:36:24.06364
14	5	2026-08-08 21:16:51.968875
15	6	2026-08-09 21:35:22.756982
16	11	2026-08-22 12:36:02.741968
17	13	2026-08-28 22:48:06.337949
18	14	2026-08-29 07:39:41.106995
19	15	2026-09-13 08:28:31.161792
20	16	2026-09-14 19:31:23.524704
\.


--
-- Data for Name: catalog_outbox_events; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.catalog_outbox_events (id, entity_type, entity_id, operation_type, payload, created_at, processed_at, attempt_count, last_attempt_at, next_attempt_at, error_message, status) FROM stdin;
1	Product	22	Update	\N	2026-09-04 10:34:23.364595	2026-09-04 10:34:31.405781	1	2026-09-04 10:34:24.909014	\N	\N	Completed
2	Product	23	Update	\N	2026-09-04 10:34:23.435063	2026-09-04 10:34:32.094723	1	2026-09-04 10:34:24.909014	\N	\N	Completed
3	Product	25	Update	\N	2026-09-04 10:34:23.436286	2026-09-04 10:34:32.505106	1	2026-09-04 10:34:24.909014	\N	\N	Completed
4	Product	16	Update	\N	2026-09-04 10:34:23.436354	2026-09-04 10:34:34.124507	1	2026-09-04 10:34:24.909014	\N	\N	Completed
5	Product	17	Update	\N	2026-09-04 10:34:23.436437	2026-09-04 10:34:34.339974	1	2026-09-04 10:34:24.909014	\N	\N	Completed
6	Product	18	Update	\N	2026-09-04 10:34:23.436459	2026-09-04 10:34:35.009185	1	2026-09-04 10:34:24.909014	\N	\N	Completed
7	Product	19	Update	\N	2026-09-04 10:34:23.436472	2026-09-04 10:34:35.232449	1	2026-09-04 10:34:24.909014	\N	\N	Completed
8	Product	20	Update	\N	2026-09-04 10:34:23.436493	2026-09-04 10:34:35.686043	1	2026-09-04 10:34:24.909014	\N	\N	Completed
9	Product	21	Update	\N	2026-09-04 10:34:23.436519	2026-09-04 10:34:36.577604	1	2026-09-04 10:34:24.909014	\N	\N	Completed
10	Product	32	Update	\N	2026-09-04 10:34:23.436531	2026-09-04 10:34:36.900936	1	2026-09-04 10:34:24.909014	\N	\N	Completed
11	Product	33	Update	\N	2026-09-04 10:34:23.436543	2026-09-04 10:34:37.372101	1	2026-09-04 10:34:24.909014	\N	\N	Completed
12	Product	34	Update	\N	2026-09-04 10:34:23.436554	2026-09-04 10:34:37.78268	1	2026-09-04 10:34:24.909014	\N	\N	Completed
13	Product	35	Update	\N	2026-09-04 10:34:23.436567	2026-09-04 10:34:38.109332	1	2026-09-04 10:34:24.909014	\N	\N	Completed
14	Product	36	Update	\N	2026-09-04 10:34:23.436578	2026-09-04 10:34:38.304367	1	2026-09-04 10:34:24.909014	\N	\N	Completed
15	Product	37	Update	\N	2026-09-04 10:34:23.436604	2026-09-04 10:34:38.71226	1	2026-09-04 10:34:24.909014	\N	\N	Completed
16	Product	24	Update	\N	2026-09-04 10:34:23.436616	2026-09-04 10:34:39.466831	1	2026-09-04 10:34:24.909014	\N	\N	Completed
17	Product	26	Update	\N	2026-09-04 10:34:23.436627	2026-09-04 10:34:42.518083	1	2026-09-04 10:34:24.909014	\N	\N	Completed
18	Product	27	Update	\N	2026-09-04 10:34:23.436638	2026-09-04 10:34:43.064109	1	2026-09-04 10:34:24.909014	\N	\N	Completed
19	Product	28	Update	\N	2026-09-04 10:34:23.436651	2026-09-04 10:34:44.209023	1	2026-09-04 10:34:24.909014	\N	\N	Completed
20	Product	29	Update	\N	2026-09-04 10:34:23.436662	2026-09-04 10:34:44.411957	1	2026-09-04 10:34:24.909014	\N	\N	Completed
21	Product	30	Update	\N	2026-09-04 10:34:23.436681	2026-09-04 10:34:50.009907	1	2026-09-04 10:34:46.358011	\N	\N	Completed
22	Product	31	Update	\N	2026-09-04 10:34:23.436692	2026-09-04 10:34:50.640824	1	2026-09-04 10:34:46.358011	\N	\N	Completed
23	Product	22	Update	\N	2026-09-04 11:23:44.716176	2026-09-04 11:23:55.874725	1	2026-09-04 11:23:47.815462	\N	\N	Completed
24	Product	23	Update	\N	2026-09-04 11:23:44.851087	2026-09-04 11:23:57.093697	1	2026-09-04 11:23:47.815462	\N	\N	Completed
25	Product	25	Update	\N	2026-09-04 11:23:44.852742	2026-09-04 11:24:06.765492	1	2026-09-04 11:23:47.815462	\N	\N	Completed
26	Product	16	Update	\N	2026-09-04 11:23:44.852832	2026-09-04 11:24:15.202764	1	2026-09-04 11:23:47.815462	\N	\N	Completed
27	Product	17	Update	\N	2026-09-04 11:23:44.852976	2026-09-04 11:24:15.715663	1	2026-09-04 11:23:47.815462	\N	\N	Completed
28	Product	18	Update	\N	2026-09-04 11:23:44.853013	2026-09-04 11:24:16.323245	1	2026-09-04 11:23:47.815462	\N	\N	Completed
29	Product	19	Update	\N	2026-09-04 11:23:44.853036	2026-09-04 11:24:16.615614	1	2026-09-04 11:23:47.815462	\N	\N	Completed
30	Product	20	Update	\N	2026-09-04 11:23:44.853078	2026-09-04 11:24:17.106341	1	2026-09-04 11:23:47.815462	\N	\N	Completed
31	Product	21	Update	\N	2026-09-04 11:23:44.853127	2026-09-04 11:24:17.418593	1	2026-09-04 11:23:47.815462	\N	\N	Completed
32	Product	32	Update	\N	2026-09-04 11:23:44.853154	2026-09-04 11:24:18.053209	1	2026-09-04 11:23:47.815462	\N	\N	Completed
33	Product	33	Update	\N	2026-09-04 11:23:44.853177	2026-09-04 11:24:18.399734	1	2026-09-04 11:23:47.815462	\N	\N	Completed
34	Product	34	Update	\N	2026-09-04 11:23:44.853198	2026-09-04 11:24:18.643516	1	2026-09-04 11:23:47.815462	\N	\N	Completed
35	Product	35	Update	\N	2026-09-04 11:23:44.853221	2026-09-04 11:24:18.963212	1	2026-09-04 11:23:47.815462	\N	\N	Completed
36	Product	36	Update	\N	2026-09-04 11:23:44.853241	2026-09-04 11:24:19.234582	1	2026-09-04 11:23:47.815462	\N	\N	Completed
37	Product	37	Update	\N	2026-09-04 11:23:44.853276	2026-09-04 11:24:19.484556	1	2026-09-04 11:23:47.815462	\N	\N	Completed
38	Product	24	Update	\N	2026-09-04 11:23:44.853305	2026-09-04 11:24:19.835987	1	2026-09-04 11:23:47.815462	\N	\N	Completed
39	Product	26	Update	\N	2026-09-04 11:23:44.853326	2026-09-04 11:24:20.083683	1	2026-09-04 11:23:47.815462	\N	\N	Completed
40	Product	27	Update	\N	2026-09-04 11:23:44.853348	2026-09-04 11:24:20.355021	1	2026-09-04 11:23:47.815462	\N	\N	Completed
41	Product	28	Update	\N	2026-09-04 11:23:44.853369	2026-09-04 11:24:20.932667	1	2026-09-04 11:23:47.815462	\N	\N	Completed
42	Product	29	Update	\N	2026-09-04 11:23:44.853392	2026-09-04 11:24:21.21101	1	2026-09-04 11:23:47.815462	\N	\N	Completed
43	Product	30	Update	\N	2026-09-04 11:23:44.85342	2026-09-04 11:24:22.770882	1	2026-09-04 11:24:21.573799	\N	\N	Completed
44	Product	31	Update	\N	2026-09-04 11:23:44.853441	2026-09-04 11:24:23.165918	1	2026-09-04 11:24:21.573799	\N	\N	Completed
\.


--
-- Data for Name: categories; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.categories (id, name, description, imageurl) FROM stdin;
7	ادوات العناية بالبشرة	تضم مستحضرات العناية بالبشرة والمكياج، بالإضافة إلى المنتجات العطرية التجميلية مثل المخمريات واللوشنات التي تجمع بين الزينة والرائحة الجذابة	/images/categories/cf9f8b28-c958-4646-9d84-15d0b8a271f9_o33.jfif
5	العطور	تشمل العطور البخاخة (الفرنسية) والزيوت العطرية المركزة (الشرقية)، وتعتمد على تراكيز مختلفة من الزيوت العطرية والكحول لتناسب الاستخدام الشخصي.	images/categories/5293bae4-271c-4b54-b9ca-19a1ef8c6108_m11.jfif
8	معطرات الجو	سوائل عطرية مخصصة للمساحات المفتوحة والمنسوجات (المفارش والستائر)، تهدف إلى إضفاء حيوية وانتعاش على هواء الغرف والمكاتب.	images/categories/d0940d19-d4fe-4915-8eaa-6f06f5e91f19_r44.jfif
6	البخور	مواد عطرية صلبة مثل خشب العود الخام أو المعمول والمبثوث، تُحرق على الفحم أو المباخر الكهربائية لتعطير الأجواء والملابس.	images/categories/111a3f30-6839-475c-95bf-4c95877e0364_n22.jfif
9	الكماليات	هذا التصنيف تجريبي	/images/categories/8238bf6c-8db5-4fc0-8b38-dfcfa3e0addc.webp
\.


--
-- Data for Name: deliveryorders; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.deliveryorders (orderid, fullname, phonenumber, secondphonenumber, governorate, city, district) FROM stdin;
14	anas	776304072	\N	أبين	جعار	انس
15	anas	776304072	\N	البيضاء	رداع	انس
16	anas	776304072	\N	حجة	ميدي	انس
17	anas	776304072	\N	أبين	لودر	انس
18	anas	776304072	\N	البيضاء	Jeddah	انس
19	anas	776304072	\N	شبوة	بيحان	انس
20	Developer	777396763	\N	حضرموت	الشحر	نثصليسضشل
21	Developer	777396763	\N	حضرموت	الشحر	محمد جمال
22	Alnhatie	774023392	\N	حضرموت	الشحر	خالد المري
23	assil almnssory	778978832	\N	حضرموت	سيئون	ناصر العولقي
24	Developer	777396763	\N	حضرموت	الشحر	محمد جمال
25	Alnhatie	774023392	\N	حضرموت	الشحر	محمد جمال
26	Alnhatie	774023392	\N	حضرموت	الشحر	محمد جمال
27	Alnhatie	774023392	\N	حضرموت	الشحر	محمد جمال
28	Alnhatie	774023392	\N	حضرموت	الشحر	محمد جمال
29	Alnhatie	774023392	\N	حضرموت	الشحر	محمد جمال
30	Alnhatie	774023392	\N	حضرموت	الشحر	محمد جمال
31	Alnhatie	774023392	\N	حضرموت	الشحر	محمد جمال
32	Alnhatie	774023392	\N	إب	العدين	محمد جمال
33	Developer	777396763	\N	حضرموت	الشحر	محمد جمال
34	Developer	777396763	\N	حضرموت	المكلا	اصيل المنصوري
35	Developer	777396763	\N	صنعاء	السبعين	اصيل المنصوري
36	Developer	777396763	\N	حضرموت	الشحر	محمد جمال
37	عبدالله محسن البكري	775537506	\N	حضرموت	الشحر	عبدالله محسن البكري
38	anas	776304072	\N	الضالع	الضالع	as
39	anas	776304072	777777777	البيضاء	رداع	لاتلتا
40	anas	776304072	455555555	لحج	الحوطة	لاتلتا
41	anas	776304072	324232321	حجة	حرض	لاتلتا
42	Abdullah	774386542	774386542	Hadhramout	Al mukalla	Mohammed
43	anas	776304072	798459459	حضرموت	الشحر	as
44	محمد معاشر	774023392	999999999	عدن	كريتر	محمد جمال
45	محمد معاشر	774023392	775614341	حضرموت	الشحر	خالد مرجان
46	محمد معاشر	774023392	999999999	حضرموت	الشحر	محمد جمال
47	محمد معاشر	774023392	999999999	حضرموت	الشحر	محمد جمال
48	محمد معاشر	774023392	445566887	عدن	التواهي	خالد العزيز
49	محمد معاشر	774023392	999999999	أبين	زنجبار	لشرقيه
50	محمد معاشر	774023392	778485446	لحج	طور الباحة	لشرقيه
51	محمد معاشر	774023392	733555223	شبوة	بيحان	محمد جمال
52	محمد معاشر	774023392	777777777	أبين	زنجبار	لشرقيه
53	Ahmed	772255334	777777777	شبوة	عسيلان	خالد مرجان
54	محمد معاشر	774023392	777777777	شبوة	بيحان	محمد جمال
55	محمد معاشر	774023392	777777777	أبين	جعار	محمد جمال
56	محمد معاشر	774023392	777777777	الحديدة	الحوك	الشارقه
57	محمد معاشر	774023392	777777777	حضرموت	غيل باوزير	خالد المري
58	محمد معاشر	774023392	777777777	شبوة	عتق	لشرقيه
59	خالد مرجان	701768813	772196515	حضرموت	الشحر	Mohammed
60	محمد معاشر	774023392	781790853	حضرموت	الشحر	احمد
61	محمد معاشر	774023392	777777777	عدن	كريتر	خالد المري
62	اصيل المنصوري	775458250	778978832	عدن	كريتر	اصيل المنصوري
63	اصيل المنصوري	775458250	778978832	حضرموت	سيئون	اصيل المنصوري
64	محمد معاشر	774023392	777777777	حضرموت	الشحر	محمد جمال
65	anas	776304072	787878787	الضالع	حريب	لاتلتا
66	محمد جمال معاشر	774023392	777777777	سقطرى	حديبو	خالد العزيز
67	محمد جمال معاشر	774023392	777777777	صعدة	كتاف	سالمين
68	محمد جمال معاشر	774023392	777777777	شبوة	عتق	محمد جمال
69	محمد جمال معاشر	774023392	777777777	الضالع	قعطبة	محمد جمال
70	Developer	777396763	777777777	إب	يريم	لاتلتا
71	أمير محمد يوسف المنصوري	773996296	777777777	شبوة	حبان	ناصر سعد
72	اصيل المنصوري	775458250	778899001	مأرب	الجوبة	سالم سعيد
73	محمد جمال معاشر	774023392	777777777	صنعاء	صنعاء القديمة	mohammed
74	محمد جمال معاشر	774023392	777777777	مأرب	حريب	خالد المري
75	محمد جمال معاشر	774023392	777777777	إب	يريم	محمد جمال
76	محمد جمال معاشر	774023392	777777777	عدن	الشيخ عثمان	محمد جمال
77	محمد جمال معاشر	774023392	777777777	حضرموت	الشحر	محمد جمال
78	محمد جمال معاشر	774023392	733333333	عدن	المعلا	خالد مرجان
79	محمد جمال معاشر	774023392	777777777	تعز	المخا	محمد جمال
80	اصيل المنصوري	775458250	778978832	صنعاء	الوحدة	ناصر العولقي
81	اصيل المنصوري	775458250	777777777	مأرب	العبدية	سالم سعيد
82	اصيل المنصوري	775458250	777883239	مأرب	حبان	سالم سعيد
83	محمد جمال معاشر	774023392	733333333	لحج	تبن	محمد جمال
84	محمد جمال معاشر	774023392	777777777	البيضاء	البيضاء	خالد المري
85	anas	776304072	787878787	حضرموت	المكلا	تمت
86	اصيل المنصوري	775458250	777564545	البيضاء	الزاهر	سالم سعيد
87	اصيل المنصوري	775458250	777777777	الضالع	الحصين	ناصر سعد
88	anas	776304072	776304072	صنعاء	السبعين	انس
89	anas	776304072	776666666	حضرموت	الشحر	تمت
90	twst	111111111	770000000	عدن	عدن	اختبار
91	Developer	777396763	787878787	شبوة	عتق	لاتلتا
92	anas	776304072	776776767	حضرموت	الشحر	صصصص
93	anas	776304072	777676767	حضرموت	المكلا	صصصص
94	anas	776304072	777888888	حضرموت	المكلا	تمت
95	anas	776304072	776666666	حضرموت	المكلا	تمت
96	anas	776304072	778888888	صنعاء	السبعين	لاتلتا
97	خالد العبثاني	777777777	778787878	تعز	الشمايتين	الاللا
98	anas	776304072	775555555	عدن	التواهي	يبلبي
99	خالد العبثاني	777777777	778888888	مأرب	العبدية	بلبلبب
100	خالد العبثاني	777777777	772222222	صعدة	كتاف	التلتال
101	anas	776304072	771111111	عدن	الشيخ عثمان	لارلار
102	anas	776304072	779999999	صنعاء	السبعين	لارلار
103	anas	776304072	771111111	صنعاء	السبعين	لاتلتا
104	anas	776304072	707070707	حضرموت	سيئون	انس
105	anas	776304072	711111111	عدن	التواهي	لارلار
106	anas	776304072	777777777	عدن	كريتر	انس
107	anas	776304072	772222222	تعز	الشمايتين	تمت
108	anas	776304072	700000000	حضرموت	المكلا	لارلار
109	anas	776304072	700000000	عدن	المعلا	تمت
110	anas	776304072	700000000	حضرموت	الشمايتين	تمت
111	anas	776304072	711111111	حضرموت	الشمايتين	تمت
112	anas	776304072	700000000	حضرموت	الشمايتين	تمت
113	anas	776304072	700000000	حضرموت	المكلا	تمت
114	anas	776304072	700000000	عدن	التواهي	تمت
115	anas	776304072	700000000	عدن	المعلا	تمت
116	anas	776304072	700000000	عدن	المعلا	تمت
117	anas	776304072	700000000	عدن	المعلا	تمت
118	محمد جمال معاشر	774023392	777777777	الحديدة	بيت الفقيه	محمد جمال
119	anas	776304072	700000000	صنعاء	المعلا	تمت
120	anas	776304072	700000000	صنعاء	المعلا	تمت
121	anas	776304072	700000000	صنعاء	المعلا	تمت
122	anas	776304072	700000000	صنعاء	المعلا	تمت
123	محمد جمال معاشر	774023392	777777777	حضرموت	الشحر	محمد جمال
124	اصيل المنصوري	775458250	777740000	البيضاء	مكيراس	انس سال
125	اصيل المنصوري	775458250	787708980	سقطرى	حديبو	انس سالم
126	محمد جمال معاشر	774023392	777777777	شبوة	عتق	محمد جمال
127	محمد جمال معاشر	774023392	777777777	أبين	زنجبار	محمد جمال
128	anas	776304072	700000000	حضرموت	المكلا	لارلار
129	محمد جمال معاشر	774023392	777777777	حضرموت	الشحر	محمد جمال
130	محمد جمال معاشر	774023392	777777777	إب	إب	محمد جمال
131	anas	776304072	777888888	حضرموت	الشحر	انلا
132	anas	776304072	700000000	تعز	القاهرة	لارلار
133	anas	776304072	700000000	حضرموت	المكلا	لارلار
135	Salem Ben jabal	778908384	778908908	حضرموت	المكلا	سالم بن جبل
136	محفوظ بن كرمان	772304265	772304265	حضرموت	الشحر	أنس الدولة
\.


--
-- Data for Name: local_site_state_snapshots; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.local_site_state_snapshots (siteid, contractversion, mode, revision, effectiveatutc, expiresatutc, sitename, siteurl, startdate, originaldurationdays) FROM stdin;
1	1	Online	45	2026-09-12 16:46:11.501964+00	2027-07-11 21:00:00+00	متجر الياقوت 	https://yagot-wppl.onrender.com	2026-07-01	375
\.


--
-- Data for Name: orderdetails; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.orderdetails (id, orderid, recipientname, recipientphone, governorate, region, district, fulladdress, paymentmethod, accountname, accountnumber, transferreferencenumber, paymentimagepath, paymentnotes, createdat, updatedat) FROM stdin;
1	34	اصيل المنصوري	778978832	حضرموت	المكلا	اصيل المنصوري	حضرموت - المكلا - اصيل المنصوري	العمقي	عبداللطيف فازع مقبل النهاري	254083800	\N	/uploads/receipts/receipt_34_538521e0.png	\N	2026-07-29 07:51:37.913421	2026-07-29 07:51:39.368716
2	35	اصيل المنصوري	778978843	صنعاء	السبعين	\N	صنعاء - السبعين	بن دول 	عبداللطيف فازع مقبل النهاري	310983002	\N	/uploads/receipts/receipt_35_85fbd939.png	ريبسرسيبرسي	2026-07-29 08:17:22.62713	2026-07-29 08:17:24.375952
\.


--
-- Data for Name: orderitems; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.orderitems (id, orderid, productid, quantity, unitprice, retail_price_id, retail_size_ml, fulfilled_quantity, unavailable_quantity) FROM stdin;
126	88	34	1	2.00	\N	\N	1	0
129	91	25	1	50.00	\N	\N	1	0
128	90	36	1	500.00	\N	\N	1	0
131	93	34	2	2.00	\N	\N	2	0
133	95	30	1	500.00	\N	\N	1	0
134	95	29	1	100.00	\N	\N	1	0
135	95	27	4	50.00	\N	\N	4	0
136	95	20	2	68.00	\N	\N	2	0
137	95	17	1	35.00	\N	\N	1	0
138	95	21	1	115.00	\N	\N	1	0
141	97	21	1	115.00	\N	\N	1	0
143	99	29	1	100.00	\N	\N	\N	1
1	1	22	1	89.00	\N	\N	\N	\N
2	1	17	3	35.00	\N	\N	\N	\N
151	102	29	1	100.00	\N	\N	1	0
181	119	21	1	115.00	\N	\N	0	1
15	10	24	1	50.00	\N	\N	\N	\N
162	105	22	1	89.00	\N	\N	1	0
163	106	22	1	89.00	\N	\N	1	0
164	107	30	1	500.00	\N	\N	1	0
155	104	29	1	100.00	\N	\N	0	1
156	104	30	1	500.00	\N	\N	1	0
157	104	21	1	115.00	\N	\N	1	0
158	104	20	1	68.00	\N	\N	1	0
159	104	22	1	89.00	\N	\N	1	0
160	104	17	1	35.00	\N	\N	1	0
161	104	25	2	50.00	\N	\N	2	0
165	108	25	1	50.00	\N	\N	0	1
35	21	21	5	115.00	\N	\N	\N	\N
36	21	20	1	68.00	\N	\N	\N	\N
37	22	21	5	115.00	\N	\N	\N	\N
166	109	30	4	500.00	\N	\N	4	0
167	109	30	16	410.00	2	150	16	0
168	110	30	1	500.00	\N	\N	0	1
176	114	30	10	410.00	2	150	10	0
182	120	37	1	20.00	\N	\N	0	1
175	113	30	10	410.00	2	150	0	10
177	115	17	1	35.00	\N	\N	0	\N
178	116	20	1	68.00	\N	\N	0	\N
180	118	21	1	115.00	\N	\N	0	\N
55	37	24	1	50.00	\N	\N	\N	\N
179	117	21	1	115.00	\N	\N	0	\N
183	120	20	1	68.00	\N	\N	1	0
184	120	17	1	35.00	\N	\N	1	0
61	42	24	1	50.00	\N	\N	\N	\N
62	42	22	1	89.00	\N	\N	\N	\N
189	123	36	1	500.00	\N	\N	1	0
187	122	37	1	20.00	\N	\N	1	0
188	122	36	4	500.00	\N	\N	4	0
190	124	20	1	68.00	\N	\N	1	0
193	126	30	4	410.00	2	150	4	0
194	127	30	1	410.00	2	150	\N	\N
195	128	37	1	20.00	\N	\N	\N	\N
196	129	20	1	68.00	\N	\N	\N	\N
197	130	24	3	50.00	\N	\N	\N	\N
198	131	37	1	20.00	\N	\N	1	0
199	132	37	1	20.00	\N	\N	\N	\N
200	133	38	1	30.00	\N	\N	\N	\N
201	135	38	1	30.00	\N	\N	\N	\N
202	136	38	1	30.00	\N	\N	\N	\N
116	80	18	2	165.00	\N	\N	\N	2
113	78	24	1	50.00	\N	\N	0	1
118	82	34	1	2.00	\N	\N	\N	0
119	82	27	1	50.00	\N	\N	\N	0
120	82	18	1	165.00	\N	\N	\N	1
125	87	20	1	68.00	\N	\N	\N	\N
34	20	23	2	50.00	\N	\N	2	0
85	55	24	5	50.00	\N	\N	5	0
86	56	26	1	5.00	\N	\N	1	0
87	56	28	1	12.00	\N	\N	1	0
88	56	19	5	130.00	\N	\N	5	0
89	56	24	1	50.00	\N	\N	1	0
90	57	21	1	115.00	\N	\N	1	0
92	59	22	1	89.00	\N	\N	1	0
93	60	22	1	89.00	\N	\N	1	0
94	61	27	3	50.00	\N	\N	3	0
95	62	24	1	50.00	\N	\N	1	0
96	62	29	1	100.00	\N	\N	1	0
97	63	30	2	410.00	2	150	2	0
98	64	22	1	89.00	\N	\N	1	0
99	65	27	1	50.00	\N	\N	1	0
100	66	24	1	50.00	\N	\N	1	0
3	2	22	1	89.00	\N	\N	\N	\N
4	3	22	1	89.00	\N	\N	\N	\N
5	4	17	1	35.00	\N	\N	\N	\N
101	67	21	1	115.00	\N	\N	1	0
102	68	22	1	89.00	\N	\N	1	0
105	71	16	1	80.00	\N	\N	1	0
111	76	24	1	50.00	\N	\N	1	0
114	79	24	1	50.00	\N	\N	0	1
115	79	22	1	89.00	\N	\N	1	0
123	85	24	1	50.00	\N	\N	1	0
6	4	16	1	80.00	\N	\N	\N	\N
7	5	17	1	35.00	\N	\N	\N	\N
8	5	16	1	80.00	\N	\N	\N	\N
9	6	17	1	35.00	\N	\N	\N	\N
10	6	16	1	80.00	\N	\N	\N	\N
11	6	23	1	50.00	\N	\N	\N	\N
12	7	18	2	165.00	\N	\N	\N	\N
16	11	24	1	50.00	\N	\N	\N	\N
17	11	21	1	115.00	\N	\N	\N	\N
18	12	24	1	50.00	\N	\N	\N	\N
19	12	20	1	68.00	\N	\N	\N	\N
20	13	18	2	165.00	\N	\N	\N	\N
21	13	20	1	68.00	\N	\N	\N	\N
22	13	17	1	35.00	\N	\N	\N	\N
23	13	22	1	89.00	\N	\N	\N	\N
24	13	19	3	130.00	\N	\N	\N	\N
25	13	24	1	50.00	\N	\N	\N	\N
26	14	21	2	115.00	\N	\N	\N	\N
27	14	24	2	50.00	\N	\N	\N	\N
28	14	22	3	89.00	\N	\N	\N	\N
29	15	24	1	50.00	\N	\N	\N	\N
30	16	24	1	50.00	\N	\N	\N	\N
31	17	24	1	50.00	\N	\N	\N	\N
32	18	24	1	50.00	\N	\N	\N	\N
33	19	24	1	50.00	\N	\N	\N	\N
38	23	21	1	115.00	\N	\N	\N	\N
39	23	24	1	50.00	\N	\N	\N	\N
40	23	16	1	80.00	\N	\N	\N	\N
41	23	20	1	68.00	\N	\N	\N	\N
42	24	24	2	50.00	\N	\N	\N	\N
43	25	21	3	115.00	\N	\N	\N	\N
44	26	17	1	35.00	\N	\N	\N	\N
45	27	22	1	89.00	\N	\N	\N	\N
46	28	22	1	89.00	\N	\N	\N	\N
47	29	24	1	50.00	\N	\N	\N	\N
48	30	24	1	50.00	\N	\N	\N	\N
49	31	24	1	50.00	\N	\N	\N	\N
50	32	18	1	165.00	\N	\N	\N	\N
51	33	24	2	50.00	\N	\N	\N	\N
52	34	19	1	130.00	\N	\N	\N	\N
53	35	18	1	165.00	\N	\N	\N	\N
54	36	24	7	50.00	\N	\N	\N	\N
56	38	24	1	50.00	\N	\N	\N	\N
57	39	17	1	35.00	\N	\N	\N	\N
58	39	21	1	115.00	\N	\N	\N	\N
59	40	20	4	68.00	\N	\N	\N	\N
60	41	24	1	50.00	\N	\N	\N	\N
65	44	17	50	35.00	\N	\N	\N	\N
66	44	25	1	50.00	\N	\N	\N	\N
67	44	24	25	50.00	\N	\N	\N	\N
68	45	27	3	50.00	\N	\N	\N	\N
69	46	25	1	50.00	\N	\N	\N	\N
70	46	24	25	50.00	\N	\N	\N	\N
71	47	24	25	50.00	\N	\N	\N	\N
72	48	26	1	5.00	\N	\N	\N	\N
73	48	27	11	50.00	\N	\N	\N	\N
74	49	21	1	115.00	\N	\N	\N	\N
75	50	27	1	50.00	\N	\N	\N	\N
76	51	27	1	50.00	\N	\N	\N	\N
77	52	26	1	5.00	\N	\N	\N	\N
78	52	18	2	165.00	\N	\N	\N	\N
79	52	24	25	50.00	\N	\N	\N	\N
80	52	22	15	89.00	\N	\N	\N	\N
81	52	21	100	115.00	\N	\N	\N	\N
82	52	20	5	68.00	\N	\N	\N	\N
83	53	19	2	130.00	\N	\N	\N	\N
84	54	27	5	50.00	\N	\N	\N	\N
91	58	24	1	50.00	\N	\N	\N	\N
103	69	22	1	89.00	\N	\N	\N	\N
104	70	25	1	50.00	\N	\N	\N	\N
106	72	16	1	80.00	\N	\N	\N	\N
107	73	29	1	100.00	\N	\N	\N	\N
108	74	29	1	100.00	\N	\N	\N	\N
109	74	24	1	50.00	\N	\N	\N	\N
110	75	29	2	100.00	\N	\N	\N	\N
112	77	24	1	50.00	\N	\N	\N	\N
117	81	18	2	165.00	\N	\N	\N	\N
124	86	22	1	89.00	\N	\N	1	0
122	84	22	1	89.00	\N	\N	1	0
121	83	22	1	89.00	\N	\N	1	0
148	101	25	1	50.00	\N	\N	1	0
149	101	22	1	89.00	\N	\N	1	0
63	43	24	2	50.00	\N	\N	0	2
64	43	18	2	165.00	\N	\N	2	0
127	89	36	1	500.00	\N	\N	0	\N
130	92	22	30	89.00	\N	\N	30	0
150	101	20	1	68.00	\N	\N	1	0
14	9	24	1	50.00	\N	\N	\N	1
139	96	27	3	50.00	\N	\N	0	3
140	96	25	1	50.00	\N	\N	1	0
132	94	34	1	2.00	\N	\N	0	1
142	98	29	1	100.00	\N	\N	1	0
144	100	25	2	50.00	\N	\N	0	1
145	100	22	1	89.00	\N	\N	0	0
146	100	29	1	100.00	\N	\N	0	0
147	100	30	1	500.00	\N	\N	0	0
152	103	29	1	100.00	\N	\N	0	1
153	103	22	1	89.00	\N	\N	0	0
154	103	25	1	50.00	\N	\N	0	0
13	8	18	2	165.00	\N	\N	\N	2
169	111	22	1	89.00	\N	\N	0	1
172	112	22	5	89.00	\N	\N	0	5
173	112	21	1	115.00	\N	\N	1	0
174	112	20	1	68.00	\N	\N	1	0
170	111	20	1	68.00	\N	\N	0	0
171	111	21	1	115.00	\N	\N	0	0
185	121	20	6	68.00	\N	\N	\N	0
186	121	17	9	35.00	\N	\N	\N	9
191	125	37	1	4.00	16	200	1	0
192	125	20	1	68.00	\N	\N	1	0
\.


--
-- Data for Name: orders; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.orders (id, userid, orderdate, totalamount, status, trackingnumber, "TimeState", notes, paymentmethod, paymentstatus, receipturl, stockdeducted, finalfulfilledamount, paymentverifiedat, paymentverifiedbyuserid, workflowstate, cancelledat, paymentreviewedat, paymentreviewedbyuserid, admin_note) FROM stdin;
43	2	2026-08-06 11:50:33.725534	430.00	Paid	YAG-11DD5F69	2026-08-28 19:07:22.396821	\N	al-amqi	Paid	/uploads/receipts/receipt_bea6a0b3.png	t	330.00	2026-08-28 19:07:22.39682	2	ConflictResolvedContinue	\N	2026-08-28 19:07:22.39682	2	\N
20	1	2026-07-27 11:34:15.463035	100.00	Shipped	YAG-992483B8	2026-07-27 17:21:12.507551	\N	\N	Unpaid	\N	t	\N	\N	\N	\N	\N	\N	\N	\N
88	2	2026-08-28 22:35:59.464257	2.00	Paid	YAG-DED92572	2026-08-28 22:43:55.784179	\N	al-amqi	Paid	\N	t	2.00	2026-08-28 22:43:55.662937	2	\N	\N	2026-08-28 22:43:55.662937	2	\N
91	1	2026-08-29 07:01:16.971297	50.00	Paid	YAG-E95DC3B5	2026-08-29 07:04:23.102396	\N	al-amqi	Paid	\N	t	50.00	2026-08-29 07:04:22.568599	2	\N	\N	2026-08-29 07:04:22.568599	2	\N
90	13	2026-08-29 06:15:54.526434	500.00	Paid	YAG-E854FE00	2026-08-29 07:05:23.718151	\N	al-amqi	Paid	\N	t	500.00	2026-08-29 07:05:23.197865	2	\N	\N	2026-08-29 07:05:23.197865	2	\N
93	2	2026-08-29 07:27:08.150558	4.00	Paid	YAG-40F6CCFC	2026-08-29 07:27:56.413293	\N	al-amqi	Paid	\N	t	4.00	2026-08-29 07:27:55.894746	2	\N	\N	2026-08-29 07:27:55.894746	2	\N
131	2	2026-09-09 18:13:21.511918	20.00	Paid	YAG-66F9AB56	2026-09-09 18:24:22.632088	\N	al-amqi	Paid	\N	t	20.00	2026-09-09 18:24:22.114981	1	\N	\N	2026-09-09 18:24:22.114981	1	\N
97	14	2026-08-29 07:46:19.662498	115.00	Cancelled	YAG-EDA5C8A2	2026-08-29 07:57:17.894871	\N	al-amqi	Paid	\N	f	115.00	2026-08-29 07:47:21.533122	2	\N	2026-08-29 07:57:17.89487	2026-08-29 07:47:21.533122	2	\N
102	2	2026-08-29 17:48:00.597523	100.00	Paid	YAG-AD9F74F7	2026-08-29 21:45:35.600644	\N	al-amqi	Paid	\N	t	100.00	2026-08-29 21:45:35.248795	2	\N	\N	2026-08-29 21:45:35.248795	2	\N
105	2	2026-08-29 19:56:35.759233	89.00	Paid	YAG-A74E449B	2026-08-29 21:46:16.439715	\N	al-amqi	Paid	\N	t	89.00	2026-08-29 21:46:16.126849	2	\N	\N	2026-08-29 21:46:16.126849	2	\N
106	2	2026-08-29 21:21:00.183747	89.00	Paid	YAG-D7E999A9	2026-08-29 21:46:22.649634	\N	al-amqi	Paid	\N	t	89.00	2026-08-29 21:46:22.370198	2	\N	\N	2026-08-29 21:46:22.370198	2	\N
107	2	2026-08-29 21:41:41.791557	500.00	Paid	YAG-DBAE8DB5	2026-08-29 21:46:27.080671	\N	al-amqi	Paid	\N	t	500.00	2026-08-29 21:46:26.732778	2	\N	\N	2026-08-29 21:46:26.732778	2	\N
132	2	2026-09-09 22:45:44.827055	20.00	Pending	YAG-CF7BA65A	2026-09-09 22:45:46.430237	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
108	2	2026-08-30 05:51:20.20275	50.00	Cancelled	YAG-83E04B2C	2026-08-30 06:53:34.4467	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-30 06:53:34.446699	2026-08-30 06:09:58.735454	2	\N
120	2	2026-08-31 05:10:34.280326	123.00	Delivered	YAG-01B0A6A0	2026-08-31 05:12:56.447993	\N	al-amqi	Paid	\N	t	103.00	2026-08-31 05:12:19.802347	2	ConflictResolvedContinue	\N	2026-08-31 05:11:44.131623	2	\N
59	6	2026-08-09 18:38:00.239022	89.00	Processed	YAG-BB8313D3	2026-08-09 18:45:52.907168	تكفى	al-amqi	Unpaid	\N	t	\N	\N	\N	\N	\N	\N	\N	\N
110	2	2026-08-30 07:46:07.252981	500.00	Cancelled	YAG-5A513E11	2026-08-30 07:52:13.091798	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-30 07:52:13.09168	2026-08-30 07:50:26.847001	2	\N
133	2	2026-09-10 18:52:58.427719	30.00	Pending	YAG-03CED8CC	2026-09-10 18:53:00.365963	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
113	2	2026-08-30 08:50:38.276906	4100.00	Cancelled	YAG-257F9574	2026-08-30 08:54:53.466934	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-30 08:54:53.466877	2026-08-30 08:53:56.498362	2	\N
114	2	2026-08-30 08:52:10.778221	4100.00	Cancelled	YAG-CB7F4639	2026-08-30 14:53:56.82308	\N	al-amqi	Paid	\N	f	4100.00	2026-08-30 08:52:31.335986	2	\N	2026-08-30 14:53:56.822953	2026-08-30 08:52:31.335986	2	\N
115	2	2026-08-30 09:53:21.757366	35.00	Cancelled	YAG-1FB8A570	2026-08-30 14:54:08.045395	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	\N	2026-08-30 14:54:08.045393	\N	\N	\N
116	2	2026-08-30 10:06:31.439113	68.00	Cancelled	YAG-609BD363	2026-08-30 14:54:15.730145	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	\N	2026-08-30 14:54:15.730144	\N	\N	\N
109	2	2026-08-30 06:47:31.286646	8560.00	Processed	YAG-2C487554	2026-08-30 14:54:56.733408	\N	al-amqi	Paid	\N	t	8560.00	2026-08-30 06:57:24.86846	2	\N	\N	2026-08-30 06:57:24.86846	2	\N
118	3	2026-08-30 11:14:09.848829	115.00	Cancelled	YAG-23236E46	2026-08-30 17:28:49.006951	\N	al-amqi	Pending	receipt_e74a193508c64737b7fc51015c374ed4.jpg	f	0.00	\N	\N	\N	2026-08-30 17:28:49.006874	\N	\N	\N
117	2	2026-08-30 10:46:57.153933	115.00	Cancelled	YAG-6D94E4EF	2026-08-30 23:48:50.575493	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	\N	2026-08-30 23:48:50.575415	\N	\N	\N
119	2	2026-08-30 23:50:06.997883	115.00	Cancelled	YAG-ED4BCAC5	2026-08-30 23:51:01.364826	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-30 23:51:01.364824	2026-08-30 23:50:42.786178	2	\N
136	16	2026-09-14 19:36:50.221908	30.00	Pending	YAG-F8915201	2026-09-14 19:36:49.709432	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
122	2	2026-08-31 08:50:28.148969	2020.00	Delivered	YAG-5C4D0489	2026-09-01 04:09:17.47634	\N	al-amqi	Paid	\N	t	2020.00	2026-09-01 04:08:31.538875	3	\N	\N	2026-09-01 04:08:31.538875	3	\N
123	3	2026-08-31 10:11:42.124251	500.00	Delivered	YAG-5036D1D6	2026-09-01 09:09:54.530912	ارجوك سرع شفنا خام	al-amqi	Paid	\N	t	500.00	2026-08-31 14:37:00.222579	5	\N	\N	2026-08-31 14:37:00.222579	5	تم الارسال مع محمد خالد  \r\n77803400
124	5	2026-09-01 12:44:32.42187	68.00	Shipped	YAG-759A1B99	2026-09-01 12:54:24.106031	اريد اطلب بسرعة	al-amqi	Paid	\N	t	68.00	2026-09-01 12:51:24.810829	5	\N	\N	2026-09-01 12:51:24.810829	5	\N
127	3	2026-09-03 17:22:05.164713	410.00	Pending	YAG-D754699B	2026-09-03 17:22:04.299904	\N	al-amqi	Pending	receipt_1f7a9bc756cc446197748a696d358fe2.webp	f	\N	\N	\N	\N	\N	\N	\N	\N
128	2	2026-09-06 06:27:44.125699	20.00	Pending	YAG-14C537AB	2026-09-06 06:27:43.589272	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
129	3	2026-09-07 08:19:15.380923	68.00	Pending	YAG-FA5CE853	2026-09-07 08:19:14.896198	\N	al-amqi	Pending	receipt_deb1282a3fcc4b09a958f89ce91ba17c.jpg	f	\N	\N	\N	\N	\N	\N	\N	\N
130	3	2026-09-08 11:52:45.151078	150.00	Pending	YAG-38759287	2026-09-08 11:52:42.476333	\N	al-amqi	Pending	receipt_1bafc31f94f14d7bb05711dcb85f5e59.webp	f	\N	\N	\N	\N	\N	\N	\N	\N
71	11	2026-08-22 09:38:02.026864	80.00	Processed	YAG-04DC2ABC	2026-08-22 09:48:22.7044	\N	al-amqi	Pending	receipt_f47b55861b1540baa96b17157b251345.png	t	\N	\N	\N	\N	\N	\N	\N	\N
76	3	2026-08-24 15:51:19.207756	50.00	Processed	YAG-9BDC3593	2026-08-25 12:04:45.467021	\N	al-amqi	Unpaid	\N	t	\N	\N	\N	\N	\N	\N	\N	\N
55	3	2026-08-09 13:03:15.873807	250.00	Processed	YAG-096713DF	2026-08-09 16:03:15.280651	\N	al-amqi	Unpaid	\N	t	250.00	\N	\N	\N	\N	\N	\N	\N
56	3	2026-08-09 14:20:44.850802	717.00	Processed	YAG-61D22342	2026-08-09 17:20:43.835295	\N	al-amqi	Unpaid	\N	t	717.00	\N	\N	\N	\N	\N	\N	\N
78	3	2026-08-27 00:38:32.707537	50.00	Cancelled	YAG-E8886DDA	2026-08-27 03:33:37.295757	\N	bin-dawl	Paid	receipt_166c00554a6540fbb61dfe56eaa66b45.jpg	f	0.00	2026-08-27 00:40:35.914132	3	ConflictResolvedCancel	2026-08-28 12:48:29.398289	2026-08-27 00:40:35.914132	3	\N
89	2	2026-08-28 22:55:31.043263	500.00	Cancelled	YAG-F128FC43	2026-08-29 07:06:07.172193	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	\N	2026-08-29 07:06:07.172144	\N	\N	\N
92	2	2026-08-29 07:08:36.235861	2670.00	Paid	YAG-5D92F0BB	2026-08-29 07:10:18.397909	\N	al-amqi	Paid	\N	t	2670.00	2026-08-29 07:10:17.486694	2	\N	\N	2026-08-29 07:10:17.486694	2	\N
100	14	2026-08-29 07:53:09.521308	789.00	Cancelled	YAG-5C537C9E	2026-08-29 07:56:14.420688	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-29 07:56:14.420518	2026-08-29 07:53:52.976769	2	\N
101	2	2026-08-29 07:53:35.805773	207.00	Cancelled	YAG-E0784D74	2026-08-29 07:56:53.866387	\N	al-amqi	Paid	\N	f	207.00	2026-08-29 07:53:46.225616	2	\N	2026-08-29 07:56:53.866385	2026-08-29 07:53:46.225616	2	\N
98	2	2026-08-29 07:51:43.206506	100.00	Cancelled	YAG-E1B3DEEC	2026-08-29 07:57:22.067942	\N	al-amqi	Paid	\N	f	100.00	2026-08-29 07:54:06.164494	2	\N	2026-08-29 07:57:22.067942	2026-08-29 07:54:06.164494	2	\N
94	2	2026-08-29 07:28:30.485415	2.00	Cancelled	YAG-89223FEB	2026-08-29 21:48:32.37862	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-29 21:48:32.3784	2026-08-29 07:28:42.484664	2	\N
103	2	2026-08-29 17:53:35.957767	239.00	Cancelled	YAG-B9E131F0	2026-08-30 06:51:22.346406	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-30 06:51:22.346324	2026-08-29 21:46:10.042141	2	\N
111	2	2026-08-30 07:54:28.360686	272.00	Cancelled	YAG-A18939B8	2026-08-30 07:59:45.60761	\N	al-amqi	Unpaid	\N	f	0.00	\N	\N	ConflictResolvedCancel	2026-08-30 07:59:45.607609	2026-08-30 07:59:29.364658	2	\N
121	2	2026-08-31 05:18:44.829093	723.00	Pending	YAG-870F5334	2026-08-31 05:19:10.666013	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	ConflictAwaitingDecision	\N	2026-08-31 05:19:10.127981	2	\N
112	2	2026-08-30 07:55:46.893729	628.00	Delivered	YAG-1D6F21B0	2026-08-31 05:50:27.998382	\N	al-amqi	Paid	\N	t	183.00	2026-08-30 07:58:39.871347	2	ConflictResolvedContinue	\N	2026-08-30 07:56:48.451512	2	\N
125	5	2026-09-01 13:04:55.308744	72.00	Shipped	YAG-F1504D9B	2026-09-01 13:06:20.931731	ياهووو	al-amqi	Paid	\N	t	72.00	2026-09-01 13:05:45.745569	5	\N	\N	2026-09-01 13:05:45.745569	5	تم الشحن الى حديبو رقم صاحب الميناء 78873789
135	15	2026-09-13 08:30:57.605263	30.00	Pending	YAG-B1DD19C8	2026-09-13 08:30:57.188916	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
86	5	2026-08-27 20:28:38.215431	89.00	Cancelled	YAG-10D8A595	2026-08-28 22:19:36.961748	\N	al-amqi	Paid	\N	f	89.00	2026-08-28 18:40:19.695775	2	\N	2026-08-28 22:19:36.961661	2026-08-28 18:40:19.695775	2	\N
85	2	2026-08-27 18:46:10.791127	50.00	Delivered	YAG-3CCEA042	2026-08-28 22:23:35.722927	\N	al-amqi	Paid	\N	t	50.00	2026-08-27 18:52:49.861228	2	\N	\N	2026-08-27 18:52:49.861228	2	\N
9	8	2026-07-17 11:58:55.414067	50.00	Pending	YAG-2A67C9EF	2026-08-31 05:17:07.746617	\N	\N	Unpaid	\N	f	\N	\N	\N	ConflictAwaitingDecision	\N	2026-08-31 05:17:07.254389	2	\N
84	3	2026-08-27 17:08:21.854144	89.00	Cancelled	YAG-CD8A7963	2026-08-28 22:23:29.553408	\N	custom-4b1d80e724d643648092211335e06cd9	Paid	receipt_9340cda714024ed8bec862fbcf7aa116.png	f	89.00	2026-08-28 18:43:35.334294	2	\N	2026-08-28 22:23:29.553407	2026-08-28 18:43:35.334294	2	\N
77	3	2026-08-27 00:37:53.20022	50.00	Pending	YAG-AEE6CE97	2026-08-27 20:51:15.137019	\N	al-amqi	Pending	receipt_a32de0175b5b4fef8d7d699a2dcf5c5d.png	f	\N	\N	\N	\N	\N	\N	\N	\N
80	5	2026-08-27 15:02:50.322744	330.00	Pending	YAG-9D962821	2026-08-27 20:51:20.827716	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	ConflictAwaitingDecision	\N	\N	\N	\N
81	5	2026-08-27 15:16:09.590313	330.00	Pending	YAG-0EAB94AF	2026-08-27 18:54:00.89509	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
82	5	2026-08-27 16:23:20.670584	217.00	Pending	YAG-39EBADCF	2026-08-29 07:06:16.784548	\N	al-amqi	Pending	receipt_7b4a956de270456a9f7c92cae91589de.png	f	\N	\N	\N	ConflictAwaitingDecision	\N	2026-08-29 07:06:16.784548	2	\N
95	2	2026-08-29 07:35:24.286881	1086.00	Paid	YAG-0F57642D	2026-08-29 07:47:20.347428	\N	al-amqi	Paid	\N	t	1086.00	2026-08-29 07:47:17.992715	2	\N	\N	2026-08-29 07:47:17.992715	2	\N
87	5	2026-08-27 20:45:28.463737	68.00	Cancelled	YAG-6659212D	2026-08-28 11:35:30.992472	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	2026-08-28 12:48:29.398289	\N	\N	\N
99	14	2026-08-29 07:52:02.14147	100.00	Pending	YAG-91EA4AE9	2026-08-29 07:54:11.783381	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	ConflictAwaitingDecision	\N	2026-08-29 07:54:11.42591	2	\N
57	3	2026-08-09 14:25:16.969032	115.00	Processed	YAG-C53A059E	2026-08-09 17:25:16.405143	\N	al-amqi	Unpaid	\N	t	115.00	\N	\N	\N	\N	\N	\N	\N
60	3	2026-08-10 16:24:20.011039	89.00	Processed	YAG-E6F72D41	2026-08-10 19:24:19.654303	\N	al-amqi	Unpaid	\N	t	89.00	\N	\N	\N	\N	\N	\N	\N
61	3	2026-08-10 17:31:19.722932	150.00	Processed	YAG-1ADE6E66	2026-08-10 20:31:19.337392	\N	al-amqi	Unpaid	\N	t	150.00	\N	\N	\N	\N	\N	\N	\N
62	5	2026-08-13 11:41:36.123652	150.00	Processed	YAG-DB01E1AA	2026-08-13 14:41:35.927052	\N	al-amqi	Unpaid	\N	t	150.00	\N	\N	\N	\N	\N	\N	\N
63	5	2026-08-13 11:55:29.936367	820.00	Processed	YAG-5C27E78F	2026-08-13 14:55:29.757944	\N	al-amqi	Pending	/uploads/receipts/receipt_26821382.png	t	820.00	\N	\N	\N	\N	\N	\N	\N
8	32	2026-07-12 17:42:05.312166	330.00	Pending	YAG-F785C377	2026-08-31 05:17:26.221976	\N	\N	Unpaid	\N	f	\N	\N	\N	ConflictAwaitingDecision	\N	2026-08-31 05:17:25.756982	2	\N
104	2	2026-08-29 17:55:00.610051	1007.00	Paid	YAG-8367F822	2026-08-30 06:03:17.152084	\N	al-amqi	Paid	\N	t	907.00	2026-08-30 06:03:17.151925	2	ConflictResolvedContinue	\N	2026-08-29 21:46:02.671028	2	\N
64	3	2026-08-14 13:03:20.345135	89.00	Processed	YAG-BCAED1C0	2026-08-14 16:03:20.087118	\N	al-amqi	Pending	receipt_0e93f81830b748d292b5bc0358afff72.png	t	89.00	\N	\N	\N	\N	\N	\N	\N
65	2	2026-08-14 16:09:11.617784	50.00	Processed	YAG-B1CDE8E8	2026-08-14 19:09:13.782395	\N	al-amqi	Pending	/uploads/receipts/receipt_8003cedb.png	t	50.00	\N	\N	\N	\N	\N	\N	\N
66	3	2026-08-16 03:52:51.812181	50.00	Processed	YAG-4B2CFEDE	2026-08-16 06:52:51.14553	\N	al-amqi	Pending	receipt_a90dff03082f48d4aa35f7d44d9bfcd3.png	t	50.00	\N	\N	\N	\N	\N	\N	\N
67	3	2026-08-16 03:57:00.639732	115.00	Processed	YAG-C3ECDE83	2026-08-16 06:57:00.205374	\N	al-amqi	Pending	receipt_693652357c0547baafffc9660137d610.png	t	115.00	\N	\N	\N	\N	\N	\N	\N
68	3	2026-08-16 12:07:04.177607	89.00	Processed	YAG-A3564417	2026-08-16 15:07:03.463294	\N	al-amqi	Unpaid	\N	t	89.00	\N	\N	\N	\N	\N	\N	\N
2	29	2026-07-10 14:48:58.507294	89.00	Pending	YAG-F2E4FEFC	2026-07-10 17:48:58.587681	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
50	3	2026-08-08 20:06:45.197345	50.00	Pending	YAG-8CD5A7B3	2026-08-08 23:06:43.052052	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
4	34	2026-07-10 15:13:56.518867	115.00	Pending	YAG-EB08F777	2026-07-10 15:44:24.647217	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
52	3	2026-08-08 20:24:15.379867	14760.00	Pending	YAG-489847FF	2026-08-08 23:24:13.276327	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
53	10	2026-08-08 20:28:09.77058	260.00	Pending	YAG-23CDEB05	2026-08-08 23:28:07.671243	\N	al-basiri	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
54	3	2026-08-09 11:32:01.443038	250.00	Pending	YAG-BAAFCC69	2026-08-09 14:32:00.722887	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
126	3	2026-09-01 13:24:26.724068	1640.00	Paid	YAG-D2AB984B	2026-09-01 13:25:58.195008	\N	al-basiri	Paid	receipt_bbef7fa79c59401884915b55178a8511.webp	t	1640.00	2026-09-01 13:25:57.718215	3	\N	\N	2026-09-01 13:25:57.718215	3	\N
3	29	2026-07-10 14:56:41.456607	89.00	Pending	YAG-AC6ACEF2	2026-07-10 19:21:46.393078	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
79	3	2026-08-27 03:43:38.647599	139.00	Processed	YAG-55ABBBF7	2026-08-27 18:56:18.369472	\N	al-amqi	Paid	\N	t	89.00	2026-08-27 03:44:44.479562	3	ConflictResolvedContinue	\N	2026-08-27 03:44:44.479562	3	\N
5	33	2026-07-10 16:35:19.070049	115.00	Pending	YAG-C8736D16	2026-07-10 19:35:16.611719	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
6	35	2026-07-10 16:56:35.476384	165.00	Pending	YAG-D8BA2AFE	2026-07-12 17:22:37.089241	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
58	3	2026-08-09 14:26:29.227638	50.00	Pending	YAG-BA70DC7A	2026-08-09 17:28:41.701892	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
7	32	2026-07-12 17:30:07.66161	330.00	Pending	YAG-070653B9	2026-07-12 17:43:53.551975	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
11	2	2026-07-22 22:49:20.298532	165.00	Pending	YAG-1CA3DD95	2026-07-23 01:49:24.668624	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
12	2	2026-07-23 20:06:23.628046	118.00	Pending	YAG-905407C3	2026-07-23 23:06:29.462766	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
13	2	2026-07-24 18:00:09.38545	962.00	Pending	YAG-5F6ACDFA	2026-07-24 21:00:16.261571	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
14	2	2026-07-25 21:50:09.085223	597.00	Pending	YAG-214E60DF	2026-07-26 00:50:17.688593	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
15	2	2026-07-25 21:54:07.508489	50.00	Pending	YAG-9DC7EFDA	2026-07-26 00:54:16.064583	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
16	2	2026-07-25 21:55:04.109035	50.00	Pending	YAG-CA6C3740	2026-07-26 00:55:12.652283	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
17	2	2026-07-25 21:58:03.331613	50.00	Pending	YAG-32CE0D99	2026-07-26 00:58:11.910385	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
18	2	2026-07-25 22:01:26.290504	50.00	Pending	YAG-31AFEAAA	2026-07-26 01:01:34.903675	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
19	2	2026-07-25 22:05:33.339369	50.00	Pending	YAG-A913E467	2026-07-26 01:05:41.895376	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
23	4	2026-07-28 03:10:22.617394	313.00	Pending	YAG-7290E447	2026-07-28 06:10:22.977233	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
24	1	2026-07-28 03:52:47.188895	100.00	Pending	YAG-FC171D08	2026-07-28 06:52:45.196215	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
25	3	2026-07-28 04:07:28.353211	345.00	Pending	YAG-CB92C801	2026-07-28 07:07:26.376871	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
26	3	2026-07-28 04:10:46.398055	35.00	Pending	YAG-BEA350A1	2026-07-28 07:10:44.327149	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
27	3	2026-07-28 04:13:33.538334	89.00	Pending	YAG-ABE39037	2026-07-28 07:13:31.40421	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
28	3	2026-07-28 04:17:00.900589	89.00	Pending	YAG-DE6F7E92	2026-07-28 07:16:58.847927	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
29	3	2026-07-28 04:19:56.848559	50.00	Pending	YAG-665335AA	2026-07-28 07:19:54.824393	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
30	3	2026-07-28 04:20:49.299476	50.00	Pending	YAG-2CB76D93	2026-07-28 07:20:47.17314	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
31	3	2026-07-28 08:06:29.709469	50.00	Pending	YAG-E5387C62	2026-07-28 11:06:27.792417	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
83	3	2026-08-27 17:07:34.586997	89.00	Delivered	YAG-DBFE2DA3	2026-08-28 18:53:11.468206	\N	al-basiri	Paid	\N	t	89.00	2026-08-28 18:53:04.26676	2	\N	\N	2026-08-28 18:53:04.26676	2	\N
32	3	2026-07-28 08:07:31.364573	165.00	Pending	YAG-29205AC8	2026-07-28 11:07:29.407517	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
33	1	2026-07-28 09:24:32.842638	100.00	Pending	YAG-3586C69E	2026-07-28 12:24:31.060594	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
34	1	2026-07-29 07:51:37.712804	130.00	Pending	YAG-E9A6A7A1	2026-07-29 10:51:40.659107	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
35	1	2026-07-29 08:17:22.364235	165.00	Pending	YAG-623F7ED3	2026-07-29 11:17:25.468455	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
36	1	2026-07-31 10:29:04.194922	350.00	Pending	YAG-89519CAC	2026-07-31 13:29:04.490669	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
69	3	2026-08-16 13:10:18.189959	89.00	Pending	YAG-6783DEE4	2026-08-16 16:31:54.024366	\N	al-amqi	Pending	receipt_8c4ed76212e04e77b788cb480c4c66e5.png	f	\N	\N	\N	\N	\N	\N	\N	\N
51	3	2026-08-08 20:21:23.242424	50.00	Pending	YAG-AC1F03E7	2026-08-11 23:36:39.795251	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
38	2	2026-07-31 19:16:48.160323	50.00	Pending	YAG-F93C9A1C	2026-07-31 22:16:48.594395	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
39	2	2026-07-31 22:31:41.269958	150.00	Pending	YAG-44018E9C	2026-08-01 01:31:45.772962	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
40	2	2026-08-01 10:52:42.491258	272.00	Pending	YAG-F53A5CD9	2026-08-01 13:52:42.812094	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
41	2	2026-08-01 11:27:14.196654	50.00	Pending	YAG-1031963C	2026-08-01 22:43:48.812154	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
44	3	2026-08-08 16:31:19.149807	3050.00	Pending	YAG-CB6DEDB9	2026-08-08 20:41:14.218076	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
45	3	2026-08-08 17:44:17.722922	150.00	Pending	YAG-78024B55	2026-08-08 20:44:15.483325	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
46	3	2026-08-08 19:50:45.923666	1300.00	Pending	YAG-F38DC779	2026-08-08 22:50:43.757536	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
47	3	2026-08-08 19:53:24.018748	1250.00	Pending	YAG-52124D5A	2026-08-08 22:53:21.888778	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
48	3	2026-08-08 20:03:30.543081	555.00	Pending	YAG-1F149490	2026-08-08 23:03:28.36718	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
49	3	2026-08-08 20:04:00.206394	115.00	Pending	YAG-14689056	2026-08-08 23:03:58.065475	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
70	1	2026-08-22 07:25:55.381655	50.00	Pending	YAG-2665080C	2026-08-22 10:25:53.541394	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
72	5	2026-08-22 09:45:58.24322	80.00	Pending	YAG-106DAE35	2026-08-22 12:45:57.752408	\N	al-amqi	Pending	receipt_b52046bece2443c192d11b5be1e4e9b1.png	f	\N	\N	\N	\N	\N	\N	\N	\N
74	3	2026-08-22 15:18:15.32163	150.00	Pending	YAG-EE889CB7	2026-08-22 18:18:14.807854	\N	custom-4b1d80e724d643648092211335e06cd9	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
73	3	2026-08-22 15:17:39.339847	100.00	Pending	YAG-24D78EE8	2026-08-22 15:22:06.665982	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
75	3	2026-08-22 15:18:59.92641	200.00	Pending	YAG-73543EB9	2026-08-22 15:22:24.268702	\N	bin-dawl	Unpaid	\N	f	\N	\N	\N	\N	\N	\N	\N	\N
1	34	2026-07-10 10:13:38.008518	194.00	Cancelled	YAG-96CFB042	2026-07-10 13:16:41.464248	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	2026-08-28 12:48:29.398289	\N	\N	\N
10	3	2026-07-22 14:15:05.158523	50.00	Cancelled	YAG-E3CDAD8A	2026-07-22 14:17:43.650739	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	2026-08-28 12:48:29.398289	\N	\N	\N
21	1	2026-07-28 01:23:31.220222	643.00	Cancelled	YAG-CB7651E1	2026-07-28 01:28:58.614998	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	2026-08-28 12:48:29.398289	\N	\N	\N
22	3	2026-07-28 01:35:00.335546	575.00	Cancelled	YAG-B41E7D07	2026-07-28 01:41:30.458168	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	2026-08-28 12:48:29.398289	\N	\N	\N
37	7	2026-07-31 12:53:33.112107	50.00	Cancelled	YAG-4CFCA974	2026-07-31 12:57:26.203754	\N	\N	Unpaid	\N	f	\N	\N	\N	\N	2026-08-28 12:48:29.398289	\N	\N	\N
42	8	2026-08-06 11:38:52.945574	139.00	Cancelled	YAG-15A1308D	2026-08-06 11:42:04.990888	\N	al-amqi	Unpaid	\N	f	\N	\N	\N	\N	2026-08-28 12:48:29.398289	\N	\N	\N
96	2	2026-08-29 07:36:45.032859	200.00	Paid	YAG-72093FED	2026-08-29 07:48:24.661368	\N	al-amqi	Paid	\N	t	50.00	2026-08-29 07:48:24.661366	2	ConflictResolvedContinue	\N	2026-08-29 07:47:27.156121	2	\N
\.


--
-- Data for Name: paymentmethods; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.paymentmethods (id, type, name, accountholdername, accountnumber, instructions, cardcolor, isactive, storesettingsid) FROM stdin;
2	al-amqi	العمقي	ياقوت	254083801	\N	#0c6b00	t	1
3	bin-dawl	بن دول	عبداللطيف فازع مقبل النهاري	310983002	\N	#1a73e8	t	1
4	al-basiri	البسيري	مؤسسة ياقوت للتجارة		\N	\N	t	1
5	other	أخرى	طريقة دفع أخرى		سيتم التواصل معك هاتفياً للاتفاق على طريقة الدفع المناسبة	\N	t	1
8	Cash	نقدي	المتجر	CASH-001	\N	\N	t	1
6	custom-4b1d80e724d643648092211335e06cd9	الكريمي	ياقوت	115	\N	\N	t	1
7	custom-00c18d4ac43e4610a9c4171bb8d9e12c	القطيبي	ياقو	324	\N	\N	t	1
\.


--
-- Data for Name: product_retail_prices; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.product_retail_prices (id, product_id, size_ml, price, is_active) FROM stdin;
4	31	120	150.00	t
5	31	100	100.00	t
13	29	100	50.00	f
12	29	150	200.00	f
14	33	50	100.00	t
15	33	75	200.00	t
2	30	150	410.00	t
16	37	200	4.00	t
17	37	300	6.00	t
\.


--
-- Data for Name: products; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.products (id, categoryid, name, description, price, stockquantity, imageurl, createdat, brand, is_retail_enabled, stock_unit, volume_ml, total_sold, sales_last_updated_at) FROM stdin;
22	8	مرش لافندر ومنت	بخاخ مخصص للمنسوجات والمفارش، يساعد على تهدئة الأعصاب وتعطير الغرف برائحة طبيعية.	89.00	0	/images/products/c33101d8-5858-4d57-9571-57d08b8c8a16_ggg.jfif	2026-05-03 01:47:30.581266	\N	f	Piece	\N	61	2026-09-04 11:23:42.003132
23	5	عطر عساف	عطر فواح	50.00	0	/images/products/17f9b608-27a8-4e67-9f0b-bcc849310188.png	2026-07-02 01:21:55.138738	\N	f	Piece	\N	2	2026-09-04 11:23:42.003132
25	5	مرحبا	\N	50.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-01 13:54:12.341048	\N	f	Piece	\N	4	2026-09-04 11:23:42.003132
16	5	عطر العود الكمبودي	زيت عطري مركز يتميز برائحة عميقة وثبات يدوم طويلاً، وهو من أرقى أنواع العطور الشرقية.	80.00	0	\N	2026-05-03 01:36:40.566472	\N	f	Piece	\N	7	2026-09-04 11:23:42.003132
17	5	عطر بخاخ بالحمضيات	عطر فرنسي خفيف يعتمد على نوتات الليمون والبرغموت، مثالي للانتعاش اليومي.	35.00	0	/images/products/65ee2a1a-fb6a-4afb-b6fd-49db3a22345b.jpg	2026-05-03 01:39:04.653021	\N	f	Piece	\N	3	2026-09-04 11:23:42.003132
18	6	معمول العروس	مزيج من كسر العود والزيوت العطرية، مصمم ليعطي دخاناً كثيفاً ورائحة زكية في المناسبات.	165.00	0	/images/products/52f24c9c-9ee2-4db2-b52a-8503388e0392_789.jfif	2026-05-03 01:40:48.848538	\N	f	Piece	\N	2	2026-09-04 11:23:42.003132
19	6	خشب العود المروكي	عود طبيعي يمتاز برائحته الهادئة (الباردة)، وهو الخيار المفضل لتبخير المجالس والبيوت.	130.00	0	/images/products/bbc50c50-af0a-42a0-9b69-7819dad294a3_abc.jfif	2026-05-03 01:42:00.555276	\N	f	Piece	\N	7	2026-09-04 11:23:42.003132
20	7	مخمرية الجسم بالزعفران	خلطة عطرية تقليدية تُوضع على نقاط النبض، تجمع بين الترطيب والرائحة المركزة التي تلتصق بالجلد.	68.00	75	/images/products/361ac92b-02a5-4a6d-b002-eb240e50ed04_def.jfif	2026-05-03 01:44:07.01842	\N	f	Piece	\N	7	2026-09-04 11:23:42.003132
38	5	عطر النحال	رائحة عسل فواح	30.00	10	/images/products/1c20b556-018c-4201-b7dc-2efedc2f14b5.webp	2026-09-08 14:55:38.832874	الماجد	f	Piece	\N	0	\N
39	7	للللللللللل	\N	20.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-09-12 02:10:42.969742	الماجد	f	Piece	\N	0	\N
40	7	تيست	تيست	1.00	1	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-09-15 20:27:54.26452	الماجد	f	Piece	\N	0	\N
21	7	لوشن المسك الأبيض	مرطب مغذٍ للبشرة يترك أثراً عطرياً ناعماً ومنعشاً يعطي شعوراً بالنظافة طوال اليوم.	115.00	0	/images/products/5228308d-fd31-440a-b2e4-0f1bff2761e7_fff.jfif	2026-05-03 01:46:03.19998	\N	f	Piece	\N	13	2026-09-04 11:23:42.003132
32	8	تيست	\N	0.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-21 00:05:41.579431	\N	f	Piece	\N	0	2026-09-04 11:23:42.003132
33	5	عين الشمس	\N	500.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-24 18:54:37.574158	\N	t	Ml	100	0	2026-09-04 11:23:42.003132
34	6	هلا	تيسست	2.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-25 11:00:50.789375	تيست	f	Piece	\N	3	2026-09-04 11:23:42.003132
35	7	هلا	لب	21.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-25 11:59:21.432992	تيست	f	Piece	\N	0	2026-09-04 11:23:42.003132
36	6	ههههههه	\N	500.00	0	/images/products/a5d9d53b-41b5-4a94-b711-74ed372a5689.png	2026-08-28 17:54:30.366412	الماجد	f	Piece	\N	6	2026-09-04 11:23:42.003132
24	5	بُرواز (Borwaz)	"الذكريات لوحات مرسومة في الذاكرة، وعطرك هو البُرواز الذي يحفظها. يعانق هذا العطر حواسك بنوتات غامضة وآسرة، ليرسم حولك هالة من الجاذبية تسبق حضورك وتبقى طويلاً بعد رحيلك. (بُرواز).. عطر يحيطك بالتميز، ويجعل من إطلالتك حكاية تروى."	50.00	3	/images/products/a2fb269c-6ede-432e-9356-cb62acc890cb.jpg	2026-07-10 23:36:07.451606	\N	f	Piece	\N	27	2026-09-04 11:23:42.003132
37	5	عطر فينيسيا	عطر فينيسيا الفخامة والعصارة	20.00	6500	/images/products/fbe3537e-334f-4dd0-9916-01ec1b264378.png	2026-08-31 03:49:59.483031	الملكي	t	Ml	1000	4	2026-09-09 20:11:07.592031
26	7	تجربة	بي	5.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-01 14:28:30.006778	الماجد	f	Piece	\N	1	2026-09-04 11:23:42.003132
27	5	تجربه 101	عنبر	50.00	0	/images/products/ed8d5d1f-c9b2-49c9-a98f-fef1aabc5ce5.webp	2026-08-08 20:43:14.869111	الملكي	f	Piece	\N	10	2026-09-04 11:23:42.003132
28	9	الخريطة	\N	12.00	0	/images/products/88de1725-81bb-409c-90e0-fb377d89e473.png	2026-08-09 14:48:38.175306	تايوان	f	Piece	\N	5	2026-09-04 11:23:42.003132
29	5	تحربة	عطر	100.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-11 01:10:25.74547	دخون الاماراتية	f	Ml	200	3	2026-09-04 11:23:42.003132
30	5	تجربة	عطر	500.00	1000	/images/products/faf96f6a-8da6-41ea-b0ff-40a7499b0591.webp	2026-08-11 01:18:36.325587	شركة دخون الاماراتية	t	Ml	1800	33	2026-09-04 11:23:42.003132
31	8	تيست	تيست	33.00	0	/images/products/6389130_camera_interface_movie_picture_zoom_icon.png	2026-08-21 00:04:27.608685	تيست	t	Ml	500	0	2026-09-04 11:23:42.003132
\.


--
-- Data for Name: sale_items; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.sale_items (id, sale_id, product_id, product_name, quantity, unit_price, discount, total, retail_price_id, retail_size_ml) FROM stdin;
1	1	16	عطر العود الكمبودي	2	80.00	0.00	160.00	\N	\N
2	2	16	عطر العود الكمبودي	2	80.00	0.00	160.00	\N	\N
4	3	28	الخريطة	1	12.00	0.00	12.00	\N	\N
306	19	22	مرش لافندر ومنت	1	89.00	0.00	89.00	\N	\N
307	20	22	مرش لافندر ومنت	1	89.00	0.00	89.00	\N	\N
82	11	19	خشب العود المروكي	1	130.00	0.00	130.00	\N	\N
20	4	28	الخريطة	2	12.00	0.00	24.00	\N	\N
83	11	19	خشب العود المروكي	1	130.00	0.00	130.00	\N	\N
88	12	28	الخريطة	1	12.00	0.00	12.00	\N	\N
328	27	24	بُرواز (Borwaz)	6	50.00	0.00	300.00	\N	\N
331	29	24	بُرواز (Borwaz)	1	50.00	0.00	50.00	\N	\N
332	29	24	بُرواز (Borwaz)	1	50.00	0.00	50.00	\N	\N
41	7	22	مرش لافندر ومنت	2	89.00	0.00	178.00	\N	\N
42	7	22	مرش لافندر ومنت	2	89.00	0.00	178.00	\N	\N
334	31	24	بُرواز (Borwaz)	1	50.00	0.00	50.00	\N	\N
335	32	24	بُرواز (Borwaz)	1	50.00	0.00	50.00	\N	\N
48	8	21	لوشن المسك الأبيض	4	115.00	0.00	460.00	\N	\N
49	8	21	لوشن المسك الأبيض	4	115.00	0.00	460.00	\N	\N
340	33	24	بُرواز (Borwaz)	1	50.00	0.00	50.00	\N	\N
55	9	22	مرش لافندر ومنت	2	89.00	0.00	178.00	\N	\N
56	9	22	مرش لافندر ومنت	2	89.00	0.00	178.00	\N	\N
344	36	22	مرش لافندر ومنت	1	89.00	0.00	89.00	\N	\N
64	10	22	مرش لافندر ومنت	3	89.00	0.00	267.00	\N	\N
65	10	22	مرش لافندر ومنت	3	89.00	0.00	267.00	\N	\N
66	10	22	مرش لافندر ومنت	3	89.00	0.00	267.00	\N	\N
123	14	16	عطر العود الكمبودي	2	80.00	0.00	160.00	\N	\N
124	14	24	بُرواز (Borwaz)	6	50.00	0.00	300.00	\N	\N
289	15	30	تجربة	2	410.00	0.00	820.00	2	150
290	15	22	مرش لافندر ومنت	1	89.00	0.00	89.00	\N	\N
291	15	27	تجربه 101	1	50.00	0.00	50.00	\N	\N
292	15	30	تجربة	2	410.00	0.00	820.00	2	150
293	15	22	مرش لافندر ومنت	1	89.00	0.00	89.00	\N	\N
294	15	27	تجربه 101	1	50.00	0.00	50.00	\N	\N
360	49	37	عطر فينيسيا	1	6.00	0.00	6.00	17	300
361	50	38	عطر النحال	1	30.00	0.00	30.00	\N	\N
\.


--
-- Data for Name: sale_payments; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.sale_payments (id, sale_id, payment_method_id, amount, transaction_reference, created_at) FROM stdin;
1	1	8	160.00	\N	2026-08-09 22:54:50.17263
2	2	8	160.00	\N	2026-08-09 22:54:50.175578
3	3	8	12.00	\N	2026-08-09 23:41:59.650642
5	4	8	24.00	\N	2026-08-09 23:44:06.376656
8	7	8	178.00	\N	2026-08-10 00:09:04.865105
10	8	2	460.00	\N	2026-08-10 00:10:21.85693
12	9	8	178.00	\N	2026-08-10 00:11:02.808104
16	10	8	89.00	\N	2026-08-10 00:20:04.398933
17	10	2	89.00	\N	2026-08-10 00:20:04.398939
18	10	6	89.00	\N	2026-08-10 00:20:04.398941
22	11	8	10.00	\N	2026-08-10 08:36:02.741724
23	11	2	100.00	\N	2026-08-10 08:36:02.741754
24	11	7	20.00	\N	2026-08-10 08:36:02.741755
25	12	8	12.00	\N	2026-08-10 09:33:34.782175
26	14	8	450.00	\N	2026-08-11 16:23:55.047397
28	15	8	959.00	\N	2026-08-12 17:37:54.494442
29	19	8	89.00	\N	2026-08-12 17:49:45.281892
30	20	8	89.00	\N	2026-08-12 17:49:45.863057
31	27	8	300.00	\N	2026-08-14 18:50:29.446457
33	29	8	50.00	\N	2026-08-14 15:55:21.746239
34	31	8	50.00	\N	2026-08-14 15:57:23.077721
35	32	8	50.00	\N	2026-08-14 15:57:24.574382
36	33	8	50.00	\N	2026-08-14 19:04:17.55079
37	36	8	89.00	\N	2026-08-14 19:09:15.853244
38	49	8	6.00	\N	2026-09-03 20:24:15.40517
\.


--
-- Data for Name: sales; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.sales (id, sales_day_id, invoice_number, customer_name, customer_phone, total_amount, discount_total, final_amount, status, notes, created_by, created_at, completed_at, updated_at, draft_revision, edit_lock_expires_at, edit_locked_by, edit_session_id) FROM stdin;
1	1	POS-1-225449-30			160.00	0.00	160.00	Completed		اصيل المنصوري	2026-08-09 22:54:49.933004	2026-08-09 22:54:50.175543	\N	0	\N	\N	\N
2	1	POS-1-225449-82			160.00	0.00	160.00	Completed		اصيل المنصوري	2026-08-09 22:54:49.768766	2026-08-09 22:54:50.175581	\N	0	\N	\N	\N
3	2	POS-2-234145-15			12.00	0.00	12.00	Completed		اصيل المنصوري	2026-08-09 23:41:45.089418	2026-08-09 23:41:59.650886	\N	0	\N	\N	\N
9	3	POS-3-001043-22			178.00	0.00	178.00	Completed		اصيل المنصوري	2026-08-10 00:10:43.963336	2026-08-10 00:11:02.808112	2026-08-10 00:10:59.24833	0	\N	\N	\N
33	5	POS-5-20260814190303-d44dcd182ec64b00877075030f3c3179			50.00	0.00	50.00	Completed		محمد معاشر	2026-08-14 19:03:03.452289	2026-08-14 19:04:17.550793	2026-08-14 19:03:50.483155	0	\N	\N	\N
14	3	POS-3-162522-93	اصيل المنصوري	775614341	460.00	10.00	450.00	Completed	مدري	محمد معاشر	2026-08-10 16:25:22.943525	2026-08-11 16:23:55.047423	2026-08-11 16:23:53.006365	0	\N	\N	\N
10	3	POS-3-001923-52			267.00	0.00	267.00	Completed		اصيل المنصوري	2026-08-10 00:19:23.521314	2026-08-10 00:20:04.398944	2026-08-10 00:20:04.129729	0	\N	\N	\N
4	2	POS-2-234228-24			24.00	0.00	24.00	Completed		اصيل المنصوري	2026-08-09 23:42:28.791638	2026-08-09 23:44:06.37667	2026-08-09 23:44:03.712597	0	\N	\N	\N
50	8	POS-8-20260909181719-5402			30.00	0.00	30.00	Draft		Developer	2026-09-09 18:17:19.385751	\N	2026-09-09 18:17:53.340216	1	\N	\N	\N
36	5	POS-5-20260814190646-806cbfbe20f54dfd9fb225dbc902e7cc			89.00	0.00	89.00	Completed		محمد معاشر	2026-08-14 19:06:46.473968	2026-08-14 19:09:15.853246	2026-08-14 19:07:33.206169	0	\N	\N	\N
27	5	POS-5-20260814184651-300ac2465fd446e2bf87636617e02344	اصيل المنصوري	778495955	300.00	0.00	300.00	Completed	بيع نقدي	محمد معاشر	2026-08-14 18:46:51.749168	2026-08-14 18:50:29.446487	2026-08-14 18:50:18.83653	0	\N	\N	\N
5	3	POS-3-235148-67	محمد صالح	778	260.00	0.00	260.00	Completed		اصيل المنصوري	2026-08-09 23:51:48.899202	2026-08-09 23:53:02.976987	2026-08-09 23:53:00.602238	0	\N	\N	\N
11	3	POS-3-004355-55			130.00	0.00	130.00	Completed		اصيل المنصوري	2026-08-10 00:43:55.504136	2026-08-10 08:36:02.741766	2026-08-10 08:35:54.458098	0	\N	\N	\N
15	3	POS-3-231631-72	222111	7789	959.00	0.00	959.00	Completed	سس	اصيل المنصوري	2026-08-11 23:16:31.999419	2026-08-12 17:37:54.494448	2026-08-12 17:36:11.789598	0	\N	\N	\N
6	3	POS-3-235359-41			356.00	0.00	356.00	Completed		اصيل المنصوري	2026-08-09 23:53:59.008954	2026-08-09 23:54:23.246491	2026-08-09 23:54:08.010645	0	\N	\N	\N
12	3	POS-3-093230-85			12.00	0.00	12.00	Completed		anas	2026-08-10 09:32:30.85438	2026-08-10 09:33:34.782187	2026-08-10 09:33:32.658959	0	\N	\N	\N
7	3	POS-3-000853-85			178.00	0.00	178.00	Completed		اصيل المنصوري	2026-08-10 00:08:53.554873	2026-08-10 00:09:04.865119	2026-08-10 00:09:00.78507	0	\N	\N	\N
29	5	POS-5-20260814155454-282			50.00	0.00	50.00	Completed		اصيل المنصوري	2026-08-14 15:54:54.695131	2026-08-14 15:55:21.746244	2026-08-14 15:54:58.737961	0	\N	\N	\N
8	3	POS-3-001002-15			460.00	0.00	460.00	Completed		اصيل المنصوري	2026-08-10 00:10:02.151757	2026-08-10 00:10:21.856934	2026-08-10 00:10:11.222068	0	\N	\N	\N
31	5	POS-5-20260814155722-238			50.00	0.00	50.00	Completed		اصيل المنصوري	2026-08-14 15:57:22.774347	2026-08-14 15:57:23.077725	\N	0	\N	\N	\N
32	5	POS-5-20260814155724-635			50.00	0.00	50.00	Completed		اصيل المنصوري	2026-08-14 15:57:24.010288	2026-08-14 15:57:24.574386	\N	0	\N	\N	\N
19	3	POS-3-20260812174945-876			89.00	0.00	89.00	Completed		اصيل المنصوري	2026-08-12 17:49:45.09649	2026-08-12 17:49:45.281975	\N	0	\N	\N	\N
20	3	POS-3-20260812174945-608			89.00	0.00	89.00	Completed		اصيل المنصوري	2026-08-12 17:49:45.096487	2026-08-12 17:49:45.863074	\N	0	\N	\N	\N
49	8	POS-8-20260903202227-f6a8	اصيل المنصوري	778495955	6.00	0.00	6.00	Completed		محمد جمال معاشر	2026-09-03 20:22:27.533749	2026-09-03 20:24:15.405226	2026-09-03 20:24:13.676696	9	\N	\N	\N
\.


--
-- Data for Name: sales_days; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.sales_days (id, date, status, opening_balance, total_sales, total_cash, total_transfer, total_wallet, total_returns, net_total, notes, created_by, closed_by, created_at, closed_at) FROM stdin;
1	2026-08-09 00:00:00	Closed	10.00	320.00	320.00	0.00	0.00	0.00	320.00	هذا المبغ جبته معي من البيت	اصيل المنصوري	اصيل المنصوري	2026-08-09 20:49:22.547633	2026-08-09 23:20:22.035582
2	2026-08-09 00:00:00	Closed	0.00	36.00	36.00	0.00	0.00	0.00	36.00	\N	اصيل المنصوري	اصيل المنصوري	2026-08-09 23:21:43.925893	2026-08-09 23:50:25.654932
3	2026-08-10 00:00:00	Closed	0.00	3428.00	2054.00	758.00	0.00	0.00	3428.00	\N	اصيل المنصوري	اصيل المنصوري	2026-08-09 23:50:56.004245	2026-08-12 18:40:34.275029
4	2026-08-12 00:00:00	Closed	0.00	0.00	0.00	0.00	0.00	0.00	0.00	\N	محمد معاشر	محمد معاشر	2026-08-12 21:25:30.78105	2026-08-14 18:33:10.061536
5	2026-08-14 00:00:00	Closed	0.00	589.00	589.00	0.00	0.00	0.00	589.00	\N	محمد معاشر	محمد معاشر	2026-08-14 18:33:24.026721	2026-08-14 21:42:33.61713
6	2026-08-14 00:00:00	Closed	0.00	0.00	0.00	0.00	0.00	0.00	0.00	\N	محمد معاشر	اصيل المنصوري	2026-08-14 21:42:51.200141	2026-08-31 20:00:58.674257
7	2026-08-31 00:00:00	Closed	0.00	0.00	0.00	0.00	0.00	0.00	0.00	\N	اصيل المنصوري	محمد جمال معاشر	2026-08-31 20:11:13.9841	2026-09-01 04:50:20.216541
8	2026-09-01 00:00:00	Open	0.00	0.00	0.00	0.00	0.00	0.00	0.00	\N	محمد جمال معاشر	\N	2026-09-01 04:51:34.554909	\N
\.


--
-- Data for Name: securitylogs; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.securitylogs (id, userid, action, ipaddress, deviceinfo, riskscore, createdat) FROM stdin;
\.


--
-- Data for Name: site_state_event_receipts; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.site_state_event_receipts (deliveryid, siteid, revision, payloadsha256, decision, recordedatutc) FROM stdin;
e30aa748-f0a9-4ef9-bde7-4a0f3923afbf	1	2	\\xa7bf71f09afb1de83819d88638d302a455b8019ec6acaa5ece65f486f3d0f46c	Applied	2026-09-09 05:36:46.334232+00
97448ada-1446-4078-a1a8-2419493be52e	1	3	\\x1aa652785f0ad7ebfaa50a58f1aec12121602bf9f93774b2d2f64e413e9f9cb3	Applied	2026-09-09 05:37:31.565027+00
e8dabaeb-fd4e-45ed-ae2a-439f03a3ea51	1	4	\\x1d8bc0ce08b218f910f28208a69f8c1856b1414a8d254b4931e2f8603a7156e5	Applied	2026-09-09 05:38:01.634005+00
ef311556-4a8e-41ed-90cb-676044d3d86c	1	5	\\x6a8ac909fc9302642e3e71df2e543218c5a75431dfbe79b893523c9127a0221a	Applied	2026-09-09 07:00:24.713579+00
ed0cb73f-f452-4747-b65b-d4a15b2b72ea	1	11	\\xd44243b3c3632f6b4cda20749c676f00b334b2df696b347bd6c0a3b98ed9228b	Applied	2026-09-11 20:27:03.510715+00
0158b725-43e8-47df-9b1b-8ac27ee940d9	1	12	\\x027197d121420ad246562ed14fc79df5d8355077d7b3c9e79b1175eb4799950e	Applied	2026-09-11 21:13:44.079683+00
0da03ef4-58bf-4ae1-be2d-85f429a8a756	1	13	\\x8cf3844ead26bca6aa96fa9b9d102462f2c4fa33ae8718d91563e97f9c7b1a12	Applied	2026-09-11 21:26:52.368321+00
cdaa6f5f-521c-48d1-afb1-4a2c65efaf1b	1	14	\\xd093444e0d523a1aa1f19949184d1eb6b272c5dd02875eb89233ef205035a843	Applied	2026-09-11 22:14:46.401405+00
94baa8ed-0d71-4c13-b904-7d8c5d2a39b5	1	15	\\x6180ab8e5c850f91046ed784c5d52b7f28c05219e6d12a3d325c95937161ce32	Applied	2026-09-11 22:15:22.549499+00
d2b4f61e-9854-4dc6-8dd7-e01bdaec96c4	1	16	\\x6f2a79210fbc6b320ec31e7577dcb68cb68547946b89d81456db1f0f89b6f7ef	Applied	2026-09-11 22:15:40.323623+00
fc516923-0c32-429c-b5b6-62b30e61568d	1	17	\\x2d903e50deb81c2b3b8782a23593f48e3bf452f1a3a4415b6c11014d05db9984	Applied	2026-09-11 22:16:39.288662+00
bf77c70d-8f1b-4ee9-b517-82ad2c93b90f	1	18	\\xf6b5571fdd0ac1f637b095f4d5f5eacc0717399b211b238d61b826c3c3247a2f	Applied	2026-09-12 05:44:12.793575+00
719dce6f-8b3e-4de8-bcd1-1dbe0c13778c	1	19	\\x72d01d81b7c0dc51c3dbf2630d2cc556819c264c780989f012a7c435925b6e83	Applied	2026-09-12 05:45:32.510794+00
8541daab-fa6b-4298-8d29-def56735c6fd	1	20	\\xec2df18b775a67ef2f5a9227a0e8f03ff6dbe7a01f7c4acd08a433827bc2e795	Applied	2026-09-12 05:49:33.158851+00
763ba64a-f5fd-414d-a6a4-5c14ed21ed12	1	21	\\xf7b5f6d18115d3cf702efb1bcbf089ffe3d3f2329a2f4138d95aa87778f9e134	Applied	2026-09-12 05:50:24.777129+00
87af66f2-cf00-4a0e-b5ee-bd21e002f832	1	22	\\x52bdaf669a28706cb551c2296ecea2e9ffd1553ea4b58aa3d949f45728d7790e	Applied	2026-09-12 06:36:41.100679+00
b95c5705-30a3-4316-be07-6456d67d35f8	1	23	\\x9a0c85ff566959e35389d4439d35c63e2c9f99db4ef7075bdfd2aa8b360a40a8	Applied	2026-09-12 06:57:02.374963+00
9ae35b84-02d8-46e7-be7b-5cfd66035899	1	24	\\xd7c9ff2a8186fa8ca04989167860889a71c96a365c0b002abd1854382dc4d103	Applied	2026-09-12 06:57:34.338153+00
0de32fcd-bc33-4e74-97eb-88e2ac31c094	1	25	\\x78978785965f2c429a9761d3c931a6ff3c1a88ce57afac2a52f70373e417946c	Applied	2026-09-12 06:57:51.012849+00
52cacf64-74d3-4a0f-837d-8c5614b01d34	1	26	\\x6d9c421080b1ebf900ee74d555c2b617cdcc5e3ff6658d4d592601608466be23	Applied	2026-09-12 07:00:05.033244+00
3c3a29f4-5f43-4d00-8aaa-69c28500aae2	1	27	\\x313479fb1b447a6b1634c99075ae0a917567d576d8d5a275a9d9d095c907dccb	EqualConflict	2026-09-12 07:21:23.521544+00
62a56bc1-68c8-4940-b355-e8f2d91cef62	1	28	\\xd7069bdd2ae12b5653ab2ac5ca758701158011d2d57449dda04a5fcbfdcd940c	Applied	2026-09-12 07:30:23.343612+00
9fef279e-611f-4ca1-8f11-fbf94fbfad98	1	29	\\x3e965a6e559f41b8574a19e550d783e7441c7d185db1ddcda787781f6bc1957c	Applied	2026-09-12 07:30:47.397514+00
0d2399ab-482e-4ba3-83fe-d7302fceb4ba	1	30	\\xc6e7b2beaafe9c867610d233dd85d824fca6983a7d2c942c89441ac645675f11	Applied	2026-09-12 07:33:01.960601+00
3bf8bb74-8429-45e3-9cba-862a450f2974	1	31	\\x9abb5b9bc9c957554eba95848e8eb2119d2286334603304b431c01abf584ce69	EqualConflict	2026-09-12 08:00:35.764709+00
b767e9f7-4e49-4366-a5a0-9138c818fcae	1	32	\\x9936e239d0e210cb82112ed4a5555be23c27ab97b1121e9a92395a4b432499a3	EqualConflict	2026-09-12 08:37:42.381406+00
4aa1e13d-2caf-46b9-86f2-4c7916421b96	1	33	\\xebf54e793fabff62bd6cc77768dc0e1ab6d0d7efaf74c2f59affa9b34291aa02	Applied	2026-09-12 08:38:54.666077+00
85da1934-1165-4137-b2aa-0501d70000e4	1	34	\\x79cb6df6bb0c3d88b0d54f35f7fba4dcb94237e0ea9e54b8b1c44bae63122a0b	Applied	2026-09-12 08:41:39.491019+00
00757c1a-6af3-4b97-b5dd-1539522a608f	1	35	\\x262614c2646e482ade2ad79ccdb0630a071cc37b89f578e2bb5c1dcd17ea26a8	Applied	2026-09-12 08:42:24.394012+00
4528cb9f-7a51-43cc-85b1-8af627de7193	1	36	\\xa85a4e8d0a0822b87d824af45eff287894b95cfc6e29eb51a2160ea8b7395e6c	Applied	2026-09-12 08:45:50.276412+00
0a67bd5c-e28a-454c-abec-a6783d7cfdf1	1	37	\\xaefb61a2e9957d9a1af860cd99e33a1b59b3e57c0b93258a5352b2927a58281d	Applied	2026-09-12 09:06:35.602719+00
2f3b69ee-1c4e-44d5-abae-a805db6b1c9d	1	38	\\xb49631f33b8539e0f925ea3e35cba3c20fd195372f961506e8feed2742a8f215	Applied	2026-09-12 09:07:31.638186+00
83e67e11-06dc-4b07-b9dd-e346eb87d547	1	40	\\xa0029b6d58c4031cbbed5eefa03c1e1b254e543a1ec605ccd82a7d1980d3f018	Applied	2026-09-12 16:39:48.750649+00
40b543f2-2cd8-47ef-b6f2-b1f4f7ab4dbe	1	39	\\x7335ba727df438fe0b9f2e9609db212a014bdc330daba38d717b3033e2c1449b	Stale	2026-09-12 16:40:33.073069+00
d580b14c-52b3-41ce-b3b6-35c897389037	1	41	\\xf05431d71e191082d37f539b7cf524095d54bd4f1246cdaf10d4e748fff78322	Applied	2026-09-12 16:42:34.450162+00
372456bb-65b4-445b-95d4-eae212289642	1	42	\\x95c7a555b8161ca925a319be4e3f1cd7639a51f9336d870cca742f344f81d4bc	Applied	2026-09-12 16:44:22.447998+00
32dea005-ec70-448b-b0c6-07b775efa146	1	43	\\x1490b2f1a57332abf5987cdc1f1335a6fa12b67cb7e9a2741220fb5ba927f1c5	Applied	2026-09-12 16:45:20.40192+00
6d51816f-664a-45e5-96a4-cf141f9d0d2b	1	44	\\xe1131bc6353a6e366e4636466304d184766989f58916431df693e2cba7d28eef	Applied	2026-09-12 16:46:01.440683+00
e420cf30-a7cf-4bba-ba76-5ffa407f069a	1	45	\\xa05826a48573e77b7b36b4104bcf119e115ae0f7046252661ea9cb52451b1e5a	Applied	2026-09-12 16:46:09.895857+00
\.


--
-- Data for Name: site_state_sync_checkpoints; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.site_state_sync_checkpoints (siteid, lastattemptatutc, lastsuccessatutc, lastobservedremoterevision, consecutivefailures, lastfailurecode, updatedatutc) FROM stdin;
1	2026-09-16 07:19:41.551262+00	2026-09-12 15:32:36.742645+00	38	20	remote_revision_older	2026-09-16 07:20:06.895696+00
\.


--
-- Data for Name: storesettings; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.storesettings (id, whatsappnumber, contactemail, instagramlink, twitterlink, tiktoklink, brandsmarquee, featuredcategoryid, heromarketingtext, heromarketingdesc) FROM stdin;
1	775458250		https://inst:tets	https://inst:tets	https://inst:tets		\N	متجر ياقوت للعطور.	هذا المتجر يقدم تشكيلة رائعة ومميزة وفريدة من اجود أنواع العطور المميزة والفريدة
\.


--
-- Data for Name: users; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.users (id, name, phone, passwordhash, role, createdat, email) FROM stdin;
34	anas	56HsGZRDFKsmqeljIJ3wpA==	lHjkTy6gQYLeAjmCXTT82g==:6r1WxvW2+J3kohIzj2KqCbK/Kdhk6g7hzqW3wOOmUdg=	Admin	2026-07-10 07:33:55.255112	xxanas.2004@gmail.com
33	محمد جمال معاشر	iuO+Zpwd81oZsxqcm8eV9A==	aJyqdUS4Pce/gsqWtL55mA==:ymbUDEppLJlCNHJ8p+QbSos1EjRJimo9H2gFZQHrNug=	Admin	2026-07-10 06:11:20.509677	tree.fiveaa@gmail.com
32	Developer	yZM7GQ9t7cFj7uotwpg0jQ==	pTb5NMp7Xw2UnV7iMzm2aw==:Hs5U5RhyGMmcanO55NjPU8FiCdOMGn8hKsbSXpE3FfQ=	Developer	2026-07-10 05:56:21.98574	assilmalmnssoty@gmail.com
29	assil almnssory	1QkcoqUu4uXPuvpg9RMhXA==	blhENQ/3RYEr+qWg30DUaw==:ud16pvNYvv+lLtWJlGADd2gPD4L2KkUkhO4bhX0qHYk=	Admin	2026-07-10 04:25:46.257406	elmansosffassel@gmail.com
36	ابو فقوص	MErcaVXGiSGLpT0yXge/hA==	CYqQESgi0KY+jVcsXNTntg==:imKwo2P2VJfZehOWHmlrs5SSXQ873Uxhpfm3ofih44o=	Customer	2026-07-12 12:14:28.368571	assilmhmdalsoty@gmail.com
35	خالد مرجان	sTMxYvT947vd7uGEDtBdtQ==	lMAIOHFs2LRJ3S3bbeP7Ow==:Zk8G6zUXKemPNH/OmjVEy8VpLvOEIp7bStRwXm97Sts=	Customer	2026-07-10 16:47:56.974758	customer_dup_35@yaqoot.local
\.


--
-- Data for Name: visits; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public.visits (id, visitdate, visitorname, country, governorate, city, device, browser) FROM stdin;
1	2026-07-10 13:16:12.704525	anas	Yemen	Aden Governorate	Aden	Desktop	Chrome
2	2026-07-10 13:28:27.003293	anas	Yemen	Aden Governorate	Aden	Desktop	Chrome
3	2026-07-10 13:43:15.336291	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Chrome
4	2026-07-10 14:05:04.170911	anas				Desktop	Chrome
5	2026-07-10 14:05:51.620469	anas				Desktop	Chrome
6	2026-07-10 14:12:39	anas	Yemen	Aden Governorate	Aden	Desktop	Chrome
7	2026-07-10 14:12:41	anas	Yemen	Aden Governorate	Aden	Desktop	Chrome
8	2026-07-10 14:38:07	assil almnssory	Yemen	Aden Governorate	Aden	K	Chrome Mobile
9	2026-07-10 14:42:42	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Chrome
10	2026-07-10 12:00:57.051151	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
11	2026-07-10 12:03:33.545098	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
12	2026-07-10 12:06:50.229264	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
13	2026-07-10 12:07:18.317673	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
14	2026-07-10 12:10:29.146212	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
15	2026-07-10 12:10:57.059058	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
16	2026-07-10 16:18:07.420131	anas				Desktop	Chrome
17	2026-07-10 16:18:30.169089	anas				Desktop	Chrome
18	2026-07-10 16:22:03.432749	anas				Desktop	Chrome
19	2026-07-10 16:31:31.744995	anas				Desktop	Chrome
20	2026-07-10 16:31:53.924047	anas				Desktop	Chrome
21	2026-07-10 16:34:34.225493	anas				Desktop	Chrome
22	2026-07-10 16:34:35.027387	anas				Desktop	Chrome
23	2026-07-10 16:48:54.282168	محمد جمال معاشر				Desktop	Chrome
24	2026-07-10 17:08:48.587083	محمد جمال معاشر				Desktop	Chrome
25	2026-07-10 14:25:35.601483	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
26	2026-07-10 14:26:13.850878	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
27	2026-07-10 14:27:38.235472	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
28	2026-07-10 14:30:37.182028	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
29	2026-07-10 17:40:05.469017	محمد جمال معاشر				Desktop	Chrome
30	2026-07-10 17:43:46.064485	محمد جمال معاشر				Desktop	Chrome
31	2026-07-10 17:44:02.695073	محمد جمال معاشر				Desktop	Chrome
32	2026-07-10 14:46:00.324782	assil almnssory	Yemen	Hadramaut Governorate	Say'un	Desktop	Chrome
33	2026-07-10 15:01:59.956528	assil almnssory	Yemen	Hadramaut Governorate	Say'un	Desktop	Chrome
34	2026-07-10 18:02:03.635816	محمد جمال معاشر				Desktop	Chrome
35	2026-07-10 15:08:48.359489	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
36	2026-07-10 15:09:29.323612	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
37	2026-07-10 15:09:30.72333	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
38	2026-07-10 15:12:01.706327	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
39	2026-07-10 15:42:21.658663	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
40	2026-07-10 15:45:01.055992	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
41	2026-07-10 18:45:51.090159	anas				Desktop	Chrome
42	2026-07-10 19:20:36.347255	محمد جمال معاشر				Desktop	Chrome
43	2026-07-10 19:21:53.805856	anas				Desktop	Chrome
44	2026-07-10 19:25:27.88199	anas				Desktop	Chrome
45	2026-07-10 16:50:40.572993	خالد مرجان	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
46	2026-07-10 16:57:46.134548	محمد جمال معاشر	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
47	2026-07-10 17:02:29.280811	خالد مرجان	Yemen	Hadramaut Governorate	Shibam	Desktop	Chrome
48	2026-07-10 17:04:14.366782	anas	United States	Illinois	Chicago	Desktop	Chrome
49	2026-07-10 17:06:55.054838	محمد جمال معاشر	Yemen	Hadramaut Governorate	Say'un	Desktop	Chrome
50	2026-07-10 17:07:22.7421	خالد مرجان	Yemen	Hadramaut Governorate	Shibam	Desktop	Edge
51	2026-07-10 20:15:39.691977	محمد جمال معاشر				Desktop	Chrome
52	2026-07-10 19:19:36.811359	محمد جمال معاشر	Yemen	Amanat Al Asimah	Sana'a	Desktop	Chrome
53	2026-07-10 19:41:31.903461	محمد جمال معاشر	Yemen	Amanat Al Asimah	Sana'a	Desktop	Chrome
54	2026-07-11 16:40:45.979715	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
55	2026-07-11 16:40:44.535035	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
56	2026-07-11 16:42:43.023495	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
57	2026-07-11 16:44:59.127249	Developer	Yemen	Aden Governorate	Aden	Desktop	Chrome
58	2026-07-12 05:43:51.26548	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Chrome
59	2026-07-12 06:05:13.191898	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Chrome
60	2026-07-12 10:08:50.424201	assil almnssory				Desktop	Chrome
61	2026-07-12 10:16:31.618143	assil almnssory				Desktop	Chrome
62	2026-07-12 10:19:21.884059	assil almnssory				Desktop	Chrome
63	2026-07-12 11:44:03.640537	anas	Yemen	Dhamar Governorate	Dhamar	Desktop	Chrome
64	2026-07-12 11:44:33.813348	anas	Yemen	Dhamar Governorate	Dhamar	Desktop	Chrome
65	2026-07-12 14:57:56.189107	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Chrome
66	2026-07-12 15:11:49.987629	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
67	2026-07-12 15:16:47.076999	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Edge
68	2026-07-12 20:19:49.837545	محمد جمال معاشر	Yemen	Aden Governorate	Aden	Desktop	Chrome
69	2026-07-12 20:20:11.070694	Developer	Yemen	Aden Governorate	Aden	Desktop	Chrome
70	2026-07-12 20:25:40.526435	Developer	Yemen	Aden Governorate	Aden	Desktop	Chrome
71	2026-07-12 22:58:44.691996	Developer	Yemen	Aden Governorate	Aden	Desktop	Chrome
72	2026-07-16 18:24:50.575875	تجربه				Desktop	Chrome
73	2026-07-17 13:31:42.390822	تجربه				Desktop	Chrome
74	2026-07-17 15:28:38.204376	Developer				Desktop	Chrome
75	2026-07-17 16:52:53.46975	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Chrome
76	2026-07-17 17:00:25.60332	assil almnssory	Yemen	Aden Governorate	Aden	Desktop	Chrome
77	2026-07-17 17:54:48.939895	assil almnssory	Yemen	Governorate Number One	Aden	Desktop	Chrome
78	2026-07-17 19:00:46.920756	anas	United States	Illinois	Chicago	Desktop	Chrome
79	2026-07-18 08:33:25.881348	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
80	2026-07-19 01:30:59.815779	anas				Desktop	Chrome
81	2026-07-19 21:22:20.812652	anas	Yemen	Sanaa	Sanaa	Desktop	Chrome
82	2026-07-20 04:03:40.169276	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
83	2026-07-20 23:01:34.287473	محمد جمال معاشر	Yemen	Sanaa	Sanaa	Desktop	Chrome
84	2026-07-20 23:01:44.853783	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
85	2026-07-21 22:05:08.448367	محمد جمال معاشر	Yemen	Sanaa	Sanaa	Desktop	Chrome
86	2026-07-21 22:25:36.554298	Developer				Desktop	Chrome
87	2026-07-22 00:54:50.151313	anas				Desktop	Chrome
88	2026-07-22 01:03:57.347537	anas				K	Chrome Mobile
89	2026-07-22 01:26:33.879951	anas				Desktop	Chrome
90	2026-07-22 02:04:35.683435	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
91	2026-07-22 16:49:27.127111	Developer	Yemen	Sanaa	Sanaa	Desktop	Chrome
92	2026-07-22 17:13:44.324498	Alnhatie	Yemen	Sanaa	Ar Rawḑah	Desktop	Chrome
93	2026-07-22 17:16:16.148655	Developer	Yemen	Sanaa	Ar Rawḑah	Desktop	Chrome
94	2026-07-23 01:49:05.813929	anas				Desktop	Chrome
95	2026-07-23 02:38:22.972494	anas				Desktop	Chrome
96	2026-07-23 04:07:14.215218	Developer	Yemen	Sanaa	Sanaa	K	Chrome Mobile
97	2026-07-23 05:13:35.183192	Developer	Yemen	Sanaa	Sanaa	Desktop	Chrome
98	2026-07-23 23:06:10.853721	anas				Samsung SM-G981B	Chrome Mobile
99	2026-07-23 23:07:13.43039	Developer				Desktop	Chrome
100	2026-07-23 23:08:57.527677	Developer	Yemen	Sanaa	Sanaa	Desktop	Chrome
101	2026-07-23 23:09:48.381053	anas	Yemen	Sanaa	Sanaa	Desktop	Chrome
102	2026-07-23 23:11:57.897198	anas	Yemen	Sanaa	Sanaa	Desktop	Chrome
103	2026-07-23 23:11:58.776121	anas	Yemen	Sanaa	Sanaa	Desktop	Chrome
104	2026-07-23 23:25:13.01743	Developer	Yemen	Sanaa	Sanaa	Desktop	Chrome
105	2026-07-24 02:34:57.700958	anas				Desktop	Edge
106	2026-07-24 03:38:47.113799	anas				Desktop	Edge
107	2026-07-24 03:42:02.654947	anas				Pixel 9	Chrome Mobile
108	2026-07-24 14:27:41.150217	anas				Desktop	Edge
109	2026-07-24 17:45:23.48568	assil almnssory				Desktop	Chrome
110	2026-07-24 20:59:42.06441	anas				Desktop	Edge
111	2026-07-24 21:06:08.164461	anas				Desktop	Edge
112	2026-07-24 21:29:18.405222	anas				Desktop	Edge
113	2026-07-24 22:03:28.448232	anas				Desktop	Edge
114	2026-07-24 22:03:28.795856	anas				Desktop	Edge
115	2026-07-25 16:58:18.930961	assil almnssory	Yemen	Hadhramaut Governorate	Hajr	K	Chrome Mobile
116	2026-07-25 22:04:48.895246	anas	Yemen	Ibb	Jiblah	Desktop	Chrome
117	2026-07-25 22:10:59.124136	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
118	2026-07-25 22:46:04.992731	anas				Desktop	Edge
119	2026-07-25 22:46:06.745861	anas				Desktop	Edge
120	2026-07-26 00:21:16.023109	anas				Desktop	Edge
121	2026-07-26 00:49:40.796223	anas				Desktop	Chrome
122	2026-07-26 00:49:42.21694	anas				Desktop	Chrome
123	2026-07-26 02:48:36.358888	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
124	2026-07-26 02:53:17.230932	anas	Yemen	Governorate Number One	Aden	Samsung SM-G981B	Chrome Mobile
125	2026-07-26 03:09:55.34121	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
126	2026-07-26 03:10:08.327318	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
127	2026-07-26 17:09:45.544691	assil almnssory				Desktop	Edge
128	2026-07-26 17:15:08.940775	Developer				Desktop	Edge
129	2026-07-26 19:43:17.168058	Developer				Desktop	Chrome
130	2026-07-26 21:33:39.011024	Developer	Yemen	Sanaa	Sanaa	K	Chrome Mobile
131	2026-07-27 07:11:43.143397	Developer				Desktop	Chrome
132	2026-07-27 07:12:54.299284	Developer				Desktop	Chrome
133	2026-07-27 07:14:09.719227	Developer				Desktop	Chrome
134	2026-07-27 07:14:57.653284	Developer				Desktop	Chrome
135	2026-07-27 07:15:23.785144	Developer				Desktop	Chrome
136	2026-07-27 13:16:01.435759	anas	United States	Nevada	Las Vegas	Desktop	Chrome
137	2026-07-27 14:11:23.622319	anas				Desktop	Chrome
138	2026-07-27 20:59:04.402149	anas				Desktop	Chrome
139	2026-07-27 20:59:05.129001	anas				Desktop	Chrome
140	2026-07-27 21:08:21.473592	anas	Yemen	Sanaa	Sanaa	Desktop	Chrome
141	2026-07-27 21:33:55.747838	anas				Desktop	Chrome
142	2026-07-27 23:58:25.557657	anas				Desktop	Chrome
143	2026-07-28 00:12:36.363874	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
144	2026-07-28 04:16:34.108903	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
145	2026-07-28 04:26:07.843609	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
146	2026-07-28 04:33:09.222057	Alnhatie	Yemen	Governorate Number One	Aden	Desktop	Chrome
147	2026-07-28 06:09:21.123703	assil almnssory	Yemen	Governorate Number One	Aden	Desktop	Edge
148	2026-07-28 06:12:21.019883	assil almnssory	Yemen	Governorate Number One	Aden	K	Chrome Mobile
149	2026-07-28 06:42:33.444098	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
150	2026-07-28 06:51:37.282163	Developer				Desktop	Chrome
151	2026-07-28 07:00:01.86808	Developer				Desktop	Chrome
152	2026-07-28 07:05:29.769367	Alnhatie				Desktop	Chrome
153	2026-07-28 09:27:52.272237	anas	Yemen	Muḩafazat Ta‘izz	Taiz	Desktop	Edge
154	2026-07-28 11:09:51.911757	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
155	2026-07-28 11:25:17.722655	Developer				Desktop	Chrome
156	2026-07-28 11:30:37.524758	Alnhatie				Desktop	Chrome
157	2026-07-28 11:49:55.284067	Developer				Desktop	Chrome
158	2026-07-28 11:56:18.556527	Alnhatie				Desktop	Chrome
159	2026-07-28 11:56:53.627434	Developer				Desktop	Chrome
160	2026-07-28 13:13:42.762904	Developer				Desktop	Chrome
161	2026-07-29 05:37:22.41794	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
162	2026-07-29 07:05:57.427341	Developer				Desktop	Chrome
163	2026-07-29 08:05:37.550639	assil almnssory				Desktop	Edge
164	2026-07-29 08:30:41.086963	Developer				Desktop	Edge
165	2026-07-29 14:31:39.541466	anas	Yemen	Muḩafazat Ḩajjah	Hajjah City	Desktop	Chrome
166	2026-07-29 21:18:00.864378	Developer	Yemen	Sanaa	Sanaa	K	Chrome Mobile
167	2026-07-29 21:29:21.195601	anas	Yemen	Muḩafazat Ḩajjah	Hajjah City	Desktop	Chrome
168	2026-07-29 21:30:05.411143	anas	Yemen	Ibb	Jiblah	Desktop	Chrome
169	2026-07-29 21:54:29.206699	anas	United States	Nevada	Las Vegas	Desktop	Chrome
170	2026-07-29 21:56:33.535052	anas	United States	Nevada	Las Vegas	Desktop	Chrome
171	2026-07-29 23:53:22.381362	anas				Desktop	Chrome
172	2026-07-29 23:54:04.13627	anas				Desktop	Chrome
173	2026-07-30 07:08:23.569307	assil almnssory				Desktop	Edge
174	2026-07-31 13:27:33.741471	Developer	Yemen	Sanaa	Sanaa	K	Chrome Mobile
175	2026-07-31 15:51:27.606579	عبدالله محسن البكري	Yemen	Sanaa	Ar Rawḑah	K	Chrome Mobile
176	2026-07-31 15:54:33.877525	Developer	Yemen	Sanaa	Sanaa	K	Chrome Mobile
177	2026-07-31 15:54:34.578201	Developer	Yemen	Sanaa	Sanaa	K	Chrome Mobile
178	2026-07-31 20:54:30.923461	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
179	2026-07-31 21:04:36.069168	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
180	2026-07-31 22:15:17.110818	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
181	2026-08-01 01:26:50.368405	anas				Desktop	Chrome
182	2026-08-01 11:54:54.001978	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
183	2026-08-01 11:54:54.01584	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
184	2026-08-01 13:51:45.112913	anas				Desktop	Chrome
185	2026-08-02 01:35:51.436412	anas	Yemen	Sanaa	Sanaa	Desktop	Chrome
186	2026-08-02 05:56:50.301024	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
187	2026-08-02 21:44:30.245925	anas				Desktop	Chrome
188	2026-08-02 21:49:33.687809	anas				Desktop	Chrome
189	2026-08-03 01:06:18.236987	anas				Desktop	Chrome
190	2026-08-03 02:24:42.121925	anas				Desktop	Chrome
191	2026-08-03 23:00:37.386077	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
192	2026-08-04 00:05:27.777707	anas				Desktop	Chrome
193	2026-08-04 00:17:05.405081	anas				Desktop	Chrome
194	2026-08-04 00:32:57.530065	anas				Desktop	Chrome
195	2026-08-04 00:57:54.856	anas				Desktop	Chrome
196	2026-08-05 15:01:11.678035	Developer	Yemen	Governorate Number One	Aden	Pixel 9	Chrome Mobile
197	2026-08-05 15:07:47.876323	Developer				Desktop	Chrome
198	2026-08-05 15:09:51.328613	خالد مرجان				Desktop	Chrome
199	2026-08-05 15:17:05.226896	Developer				Desktop	Chrome
200	2026-08-05 20:43:42.018927	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
201	2026-08-05 20:44:41.740049	خالد مرجان	Yemen	Governorate Number One	Aden	Desktop	Chrome
202	2026-08-05 20:49:24.068114	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
203	2026-08-05 22:08:54.942852	anas	Yemen	Ibb	Jiblah	Desktop	Chrome
204	2026-08-05 22:09:13.551413	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
205	2026-08-05 22:13:57.35422	خالد مرجان				Desktop	Chrome
206	2026-08-05 22:38:22.726255	anas	Yemen	Ibb	Jiblah	Desktop	Chrome
207	2026-08-05 22:57:12.34533	Developer				Desktop	Chrome
208	2026-08-06 14:40:28.159828	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
209	2026-08-06 14:49:28.663863	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
210	2026-08-06 15:20:02.82983	Developer	Yemen	Muḩafazat Ta‘izz	Taiz	K	Chrome Mobile
211	2026-08-06 18:05:22.175277	Abdullah	Yemen	Muḩafazat Ta‘izz	Taiz	K	Chrome Mobile
212	2026-08-06 19:20:08.919857	anas	Yemen	Sanaa	Sanaa	K	Chrome Mobile
213	2026-08-07 17:34:41.616805	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
214	2026-08-07 17:34:41.523693	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
215	2026-08-07 17:37:15.118309	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
216	2026-08-07 17:37:15.825063	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
217	2026-08-07 22:51:21.243376	Developer				Desktop	Chrome
218	2026-08-08 00:42:45.343337	Developer				Samsung SM-G981B	Chrome Mobile
219	2026-08-08 02:26:13.086651	Developer				Desktop	Chrome
220	2026-08-08 02:45:24.434579	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
221	2026-08-08 10:00:52.034531	أمير محمد يوسف المنصوري				Desktop	Edge
222	2026-08-08 10:01:18.601094	أمير محمد يوسف المنصوري				Desktop	Edge
223	2026-08-08 10:16:02.148518	ابو اصيل المنصوري				Desktop	Edge
224	2026-08-08 10:21:39.197899	اصيل المنصوري				Desktop	Edge
225	2026-08-08 10:23:16.148212	اصيل المنصوري				Desktop	Edge
226	2026-08-08 10:33:43.269093	اصيل المنصوري				Desktop	Edge
227	2026-08-08 10:36:53.539227	Developer				Desktop	Edge
228	2026-08-08 10:37:14.311822	اصيل المنصوري				Desktop	Edge
229	2026-08-08 10:38:51.16742	اصيل المنصوري				Desktop	Edge
230	2026-08-08 14:22:27.324783	خالد مرجان	Yemen	Governorate Number One	Aden	Desktop	Chrome
231	2026-08-08 14:27:36.241879	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
232	2026-08-08 14:29:19.996319	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
233	2026-08-08 14:45:18.463344	Abdullah	Yemen	Governorate Number One	Aden	Desktop	Chrome
234	2026-08-08 15:39:11.419593	Developer				Desktop	Chrome
235	2026-08-08 16:30:09.307059	Developer				Desktop	Chrome
236	2026-08-08 16:32:14.078455	Ahmed				Desktop	Chrome
237	2026-08-08 16:33:39.837587	Ahmed				Desktop	Chrome
238	2026-08-08 17:02:15.248437	Ahmed	Yemen	Governorate Number One	Aden	Desktop	Chrome
239	2026-08-08 17:28:29.899223	Ahmed	Yemen	Governorate Number One	Aden	Desktop	Chrome
240	2026-08-08 18:18:44.377765	TRANKSFF FREE	Yemen	Governorate Number One	Aden	Desktop	Chrome
241	2026-08-08 18:19:08.666778	Abdullah	Yemen	Governorate Number One	Aden	Desktop	Chrome
242	2026-08-08 18:19:28.86584	Abdullah	Yemen	Governorate Number One	Aden	Desktop	Chrome
243	2026-08-08 18:21:48.311488	Abdullah	Yemen	Governorate Number One	Aden	Desktop	Chrome
244	2026-08-08 18:22:05.319723	TRANKSFF FREE	Yemen	Governorate Number One	Aden	Desktop	Chrome
245	2026-08-08 18:22:26.622538	TRANKSFF FREE	Yemen	Governorate Number One	Aden	Desktop	Chrome
246	2026-08-08 18:23:00.457034	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
247	2026-08-08 19:16:52.247408	محمد معاشر				Desktop	Chrome
248	2026-08-08 19:21:15.036061	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
249	2026-08-08 21:16:47.366568	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Edge
250	2026-08-08 22:49:28.31293	محمد معاشر				Desktop	Chrome
251	2026-08-08 23:26:49.368826	Ahmed				Desktop	Chrome
252	2026-08-08 23:27:25.539811	Ahmed				Desktop	Chrome
253	2026-08-08 23:28:41.729817	Ahmed				Desktop	Chrome
254	2026-08-08 23:28:50.2145	محمد معاشر				Desktop	Chrome
255	2026-08-09 12:35:50.695717	محمد معاشر				Desktop	Chrome
256	2026-08-09 14:26:10.319191	محمد معاشر				Desktop	Chrome
257	2026-08-09 14:46:51.813912	اصيل المنصوري				Desktop	Edge
258	2026-08-09 21:35:23.964375	خالد مرجان	Yemen	Governorate Number One	Aden	K	Chrome Mobile
259	2026-08-09 21:41:26.655562	خالد مرجان	Yemen	Governorate Number One	Aden	K	Chrome Mobile
260	2026-08-09 21:44:01.273235	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
261	2026-08-09 22:57:10.387026	خالد مرجان	Yemen	Sanaa	Sanaa	K	Chrome Mobile
262	2026-08-10 00:14:28.943851	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
263	2026-08-10 10:01:24.813791	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
264	2026-08-10 10:59:21.762586	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
265	2026-08-10 10:59:33.297233	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
266	2026-08-10 12:07:37.291327	anas	Yemen	Ibb	Jiblah	Desktop	Chrome
267	2026-08-10 12:09:44.335189	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
268	2026-08-10 12:21:02.753512	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Edge
269	2026-08-10 15:09:28.679906	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
270	2026-08-10 18:28:44.652979	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
271	2026-08-10 19:22:58.612363	محمد معاشر	Yemen	Governorate Number One	Aden	K	Chrome Mobile
272	2026-08-10 20:10:13.726886	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
273	2026-08-10 20:30:10.894158	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
274	2026-08-11 11:23:07.478075	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
275	2026-08-11 19:20:38.056354	محمد معاشر	Yemen	Governorate Number One	Aden	Pixel 9	Chrome Mobile
276	2026-08-11 22:43:23.929011	anas	Yemen	Muḩafazat Ta‘izz	Taiz	Desktop	Chrome
277	2026-08-12 02:12:54.808084	أمير محمد يوسف المنصوري	Yemen	Governorate Number One	Aden	Desktop	Edge
278	2026-08-12 02:13:24.659558	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Edge
279	2026-08-12 02:22:42.381703	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
280	2026-08-12 02:32:43.678198	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
281	2026-08-12 03:04:22.854184	محمد معاشر				Desktop	Chrome
282	2026-08-12 18:46:41.51959	anas				Desktop	Edge
283	2026-08-12 19:48:06.983326	anas				Desktop	Chrome
284	2026-08-12 20:36:53.82376	anas	Yemen	Sanaa	Sanaa	Desktop	Edge
285	2026-08-12 21:17:23.438787	anas				Desktop	Edge
286	2026-08-12 21:28:28.593964	anas				Desktop	Edge
287	2026-08-13 00:17:19.117491	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
288	2026-08-13 14:06:05.547558	anas				Desktop	Chrome
289	2026-08-13 15:00:46.249311	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
290	2026-08-13 15:17:56.442141	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
291	2026-08-13 15:21:50.415923	اصيل المنصوري				Desktop	Edge
292	2026-08-13 22:15:26.698622	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
293	2026-08-14 02:01:00.193358	محمد معاشر	Yemen	Governorate Number One	Aden	K	Chrome Mobile
294	2026-08-14 13:50:24.49938	محمد معاشر	Yemen	Governorate Number One	Aden	K	Chrome Mobile
295	2026-08-14 15:13:09.6735	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
296	2026-08-14 15:13:47.694051	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
297	2026-08-14 15:14:27.289291	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
298	2026-08-14 15:14:54.789975	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
299	2026-08-14 15:15:18.695795	Developer	Yemen	Governorate Number One	Aden	Desktop	Chrome
300	2026-08-14 15:15:55.59812	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
301	2026-08-14 16:02:20.336109	محمد معاشر				Desktop	Chrome
302	2026-08-14 16:02:37.599226	محمد معاشر				Desktop	Chrome
303	2026-08-14 16:08:23.973328	Developer				Desktop	Chrome
304	2026-08-14 16:28:57.827782	محمد معاشر				Desktop	Chrome
305	2026-08-14 16:56:18.210941	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
306	2026-08-14 17:11:56.409871	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
307	2026-08-14 17:12:30.27714	محمد معاشر				Desktop	Chrome
308	2026-08-14 17:13:06.336531	محمد معاشر				Desktop	Chrome
309	2026-08-14 17:19:08.394438	محمد معاشر				Desktop	Chrome
310	2026-08-14 18:03:21.677411	محمد معاشر				Desktop	Chrome
311	2026-08-14 18:07:26.348409	محمد معاشر	Yemen	Sanaa	Sanaa	K	Chrome Mobile
312	2026-08-14 18:07:44.248244	اصيل المنصوري	Yemen	Sanaa	Sanaa	Desktop	Chrome
313	2026-08-14 18:13:02.711313	محمد معاشر				Desktop	Chrome
314	2026-08-14 18:46:40.983059	anas				Desktop	Chrome
315	2026-08-14 18:53:08.20893	اصيل المنصوري	Yemen	Sanaa	Sanaa	Desktop	Chrome
316	2026-08-14 21:46:17.781688	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
317	2026-08-14 23:34:16.721263	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
318	2026-08-15 00:03:48.245488	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
319	2026-08-15 00:08:58.321933	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
320	2026-08-15 00:10:44.820886	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
321	2026-08-15 00:25:34.990756	محمد معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
322	2026-08-15 00:27:36.499867	محمد علي العمودي	Yemen	Governorate Number One	Aden	Desktop	Chrome
323	2026-08-15 00:30:39.473398	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
324	2026-08-15 00:36:15.152396	Developer				Desktop	Chrome
325	2026-08-15 01:05:56.421561	محمد علي العمودي	Yemen	Governorate Number One	Aden	Desktop	Chrome
326	2026-08-15 01:25:55.682048	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
327	2026-08-15 01:28:38.335012	محمد علي العمودي	Yemen	Governorate Number One	Aden	Desktop	Chrome
328	2026-08-15 01:29:35.837395	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
329	2026-08-15 01:30:50.488095	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
330	2026-08-15 01:32:32.002378	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
331	2026-08-15 01:33:04.30677	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
332	2026-08-15 01:34:24.735203	محمد علي العمودي	Yemen	Governorate Number One	Aden	Desktop	Chrome
333	2026-08-15 01:35:12.853787	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
334	2026-08-15 02:08:03.155035	محمد جمال معاشر	Germany	Hessen	Limburg an der Lahn	Desktop	Chrome
335	2026-08-15 13:15:59.691783	anas	Yemen	Muḩafazat Ta‘izz	Taiz	Desktop	Edge
336	2026-08-15 13:43:01.96551	محمد جمال معاشر	Yemen	Governorate Number One	Aden	K	Chrome Mobile
337	2026-08-15 15:25:14.5187	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Samsung SM-G955U	Chrome Mobile
338	2026-08-16 00:27:39.013231	محمد جمال معاشر	Yemen	Governorate Number One	Aden	K	Chrome Mobile
339	2026-08-16 05:26:17.01392	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
340	2026-08-16 05:34:24.302834	Ahmed	Yemen	Governorate Number One	Aden	Desktop	Edge
341	2026-08-16 06:52:09.490566	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
342	2026-08-16 06:55:39.350273	محمد جمال معاشر				Desktop	Chrome
343	2026-08-16 12:13:48.279339	أمير محمد يوسف المنصوري	Yemen	Governorate Number One	Aden	Desktop	Edge
344	2026-08-16 12:55:48.66381	محمد جمال معاشر	Yemen	Governorate Number One	Aden	K	Chrome Mobile
345	2026-08-16 12:56:30.862075	Ahmed	Yemen	Governorate Number One	Aden	K	Chrome Mobile
346	2026-08-16 15:05:12.356361	محمد جمال معاشر				Desktop	Chrome
347	2026-08-16 17:19:03.617541	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
348	2026-08-16 21:53:03.733896	anas	Yemen	Muḩafazat Ta‘izz	Taiz	Samsung SM-G981B	Chrome Mobile
349	2026-08-17 15:33:59.387858	أمير محمد يوسف المنصوري				Desktop	Chrome
350	2026-08-17 15:39:49.828122	اصيل المنصوري				Desktop	Edge
351	2026-08-17 21:20:05.834936	anas	Yemen	Sanaa	Sanaa	K	Chrome Mobile
353	2026-08-18 22:07:51.569006	anas				Desktop	Chrome
352	2026-08-18 22:07:51.569051	anas				Desktop	Chrome
354	2026-08-18 22:15:55.975102	anas	United States	District of Columbia	Washington	Desktop	Chrome
355	2026-08-19 11:31:21.0049	Developer				Desktop	Chrome
356	2026-08-20 21:58:38.685091	محمد جمال معاشر	United States	District of Columbia	Washington	Desktop	Chrome
357	2026-08-20 22:30:28.685353	anas	Yemen	Sanaa	Sanaa	Desktop	Chrome
358	2026-08-21 21:10:57.290747	anas	Yemen	Governorate Number One	Aden	Desktop	Chrome
359	2026-08-22 10:23:39.074249	Developer				Desktop	Chrome
360	2026-08-22 12:34:35.69531	أمير محمد يوسف المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
361	2026-08-22 12:41:26.486113	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
362	2026-08-22 16:39:37.187341	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
363	2026-08-22 17:53:05.334254	محمد جمال معاشر	Germany	Hessen	Limburg an der Lahn	Desktop	Chrome
364	2026-08-23 22:12:57.883871	anas				Desktop	Edge
365	2026-08-24 15:48:11.73604	اصيل المنصوري				Desktop	Chrome
366	2026-08-24 18:36:47.933578	Developer	United States	District of Columbia	Washington	Desktop	Chrome
367	2026-08-24 18:50:34.815409	محمد جمال معاشر	Yemen	Sanaa	Sanaa	K	Chrome Mobile
368	2026-08-24 21:43:30.361498	anas	Yemen	Muḩafazat al Ḩudaydah	Ḩays	K	Chrome Mobile
369	2026-08-25 08:29:42.205477	anas	Yemen	Muḩafazat Ta‘izz	Ar Rawnah	Desktop	Edge
370	2026-08-27 03:52:49.218851	محمد جمال معاشر				Desktop	Chrome
371	2026-08-27 18:01:24.939966	اصيل المنصوري				Desktop	Edge
372	2026-08-28 10:24:05.863768	محمد جمال معاشر				Desktop	Chrome
373	2026-08-28 11:06:01.421295	محمد جمال معاشر				Desktop	Chrome
374	2026-08-28 11:10:26.502475	محمد جمال معاشر				Desktop	Chrome
375	2026-08-28 16:56:43.185394	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
376	2026-08-28 19:09:15.848116	اصيل المنصوري				Desktop	Chrome
377	2026-08-28 19:21:43.040044	محمد جمال معاشر	Yemen	Sanaa	Sanaa	K	Chrome Mobile
378	2026-08-28 21:42:04.201177	anas				Desktop	Chrome
379	2026-08-28 21:42:05.320037	anas				Desktop	Chrome
380	2026-08-29 01:46:42.598655	anas	Yemen	Governorate Number One	Aden	Desktop	Edge
381	2026-08-29 10:40:31.932329	محمد جمال معاشر	Yemen	Governorate Number One	Aden	K	Chrome Mobile
382	2026-08-29 10:45:50.657578	خالد العبثاني				Desktop	Edge
383	2026-08-29 10:46:50.104604	anas				Desktop	Edge
384	2026-08-29 10:50:13.504768	خالد العبثاني				Desktop	Chrome
385	2026-08-29 20:57:06.351807	anas				Desktop	Edge
386	2026-08-31 22:04:16.98796	اصيل المنصوري				Desktop	Chrome
387	2026-08-31 22:22:15.809451	اصيل المنصوري				Desktop	Chrome
388	2026-09-01 06:56:42.963931	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
389	2026-09-02 10:19:25.085058	اصيل المنصوري				Desktop	Chrome
390	2026-09-02 18:31:58.800547	اصيل المنصوري				Desktop	Chrome
391	2026-09-03 00:45:01.764385	anas				Desktop	Edge
392	2026-09-03 12:42:53.426794	anas	Yemen	Muḩafazat Ta‘izz	Taiz	Desktop	Edge
393	2026-09-03 14:51:29.242264	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
394	2026-09-03 17:01:34.601419	اصيل المنصوري				Desktop	Chrome
395	2026-09-03 20:20:30.467661	محمد جمال معاشر				Desktop	Chrome
396	2026-09-03 20:20:32.540942	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
397	2026-09-03 20:28:25.28673	محمد جمال معاشر				Desktop	Chrome
398	2026-09-03 23:15:06.036821	anas	Yemen	Governorate Number One	Aden	K	Chrome Mobile
399	2026-09-04 21:20:34.025034	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
400	2026-09-05 18:49:15.542861	anas				Pixel 9	Edge
401	2026-09-05 19:32:05.39357	anas				Desktop	Edge
402	2026-09-05 21:16:46.511925	anas				Desktop	Chrome
403	2026-09-06 11:12:40.190255	محمد علي العمودي	Yemen	Governorate Number One	Aden	K	Chrome Mobile
404	2026-09-06 11:15:42.654939	محمد علي العمودي	Yemen	Governorate Number One	Aden	K	Chrome Mobile
405	2026-09-06 18:37:57.841266	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
406	2026-09-06 18:47:26.067139	أمير محمد يوسف المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
407	2026-09-07 09:16:57.113336	anas				Pixel 9	Edge
408	2026-09-07 20:14:28.070827	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
409	2026-09-09 01:03:43.377247	anas				Desktop	Edge
410	2026-09-09 08:39:05.892663	anas				Desktop	Edge
411	2026-09-09 09:14:57.218431	anas				Desktop	Edge
412	2026-09-09 21:11:04.367403	anas	Yemen	Governorate Number One	Aden	K	Chrome Mobile
413	2026-09-09 21:16:34.936791	Developer	Yemen	Governorate Number One	Aden	K	Chrome Mobile
414	2026-09-10 01:37:00.211435	anas				Desktop	Edge
415	2026-09-11 21:55:24.240797	Developer				Desktop	Chrome
416	2026-09-11 22:00:24.480366	أمير محمد يوسف المنصوري				Desktop	Chrome
417	2026-09-11 22:26:05.943957	anas	Yemen	Governorate Number One	Aden	Desktop	Edge
418	2026-09-11 22:31:17.175262	anas				Desktop	Edge
419	2026-09-11 22:38:59.888339	anas				Desktop	Edge
420	2026-09-11 23:10:23.159786	anas	Yemen	Governorate Number One	Aden	Desktop	Edge
421	2026-09-11 23:27:50.377163	anas	Yemen	Governorate Number One	Aden	Desktop	Edge
422	2026-09-12 00:09:11.338143	anas				Desktop	Edge
423	2026-09-12 00:10:14.699646	Developer				Desktop	Edge
424	2026-09-12 00:12:39.281506	anas	Yemen	Governorate Number One	Aden	Desktop	Edge
425	2026-09-12 00:13:09.062899	anas				Desktop	Edge
426	2026-09-12 00:16:38.890318	anas				Desktop	Edge
427	2026-09-12 00:18:56.884311	anas				Desktop	Edge
428	2026-09-12 00:19:28.78698	anas				Desktop	Edge
429	2026-09-12 00:24:56.473383	anas				Desktop	Edge
430	2026-09-12 00:25:40.627444	Developer				Desktop	Edge
431	2026-09-12 00:26:26.684422	anas				Desktop	Edge
432	2026-09-12 00:27:11.482245	anas				Desktop	Edge
433	2026-09-12 00:33:22.108219	anas				Desktop	Edge
434	2026-09-12 01:11:50.451757	محمد جمال معاشر				Desktop	Chrome
435	2026-09-12 01:14:33.007746	محمد جمال معاشر				Desktop	Chrome
436	2026-09-12 01:16:07.559086	محمد جمال معاشر	Yemen	Governorate Number One	Aden	Desktop	Chrome
437	2026-09-12 03:22:09.946094	Developer				Desktop	Chrome
438	2026-09-12 08:45:51.186644	اصيل المنصوري				Desktop	Chrome
439	2026-09-12 10:00:32.51161	اصيل المنصوري				Desktop	Chrome
440	2026-09-12 10:32:35.177592	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
441	2026-09-12 10:33:32.760605	anas	Yemen	Sanaa	Sanaa	Desktop	Edge
442	2026-09-12 10:34:38.754883	Developer	Yemen	Sanaa	Sanaa	Desktop	Edge
443	2026-09-12 10:35:45.607561	anas	Yemen	Sanaa	Sanaa	Desktop	Edge
444	2026-09-14 00:02:46.354363	اصيل المنصوري	Yemen	Governorate Number One	Aden	Desktop	Chrome
\.


--
-- Name: DataProtectionKeys_Id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public."DataProtectionKeys_Id_seq"', 1, true);


--
-- Name: UserSite_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public."UserSite_id_seq"', 21, true);


--
-- Name: cartitems_id_identity_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.cartitems_id_identity_seq', 280, true);


--
-- Name: cartitems_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.cartitems_id_seq', 20, false);


--
-- Name: carts_id_identity_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.carts_id_identity_seq', 20, true);


--
-- Name: carts_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.carts_id_seq', 14, false);


--
-- Name: catalog_outbox_events_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.catalog_outbox_events_id_seq', 44, true);


--
-- Name: categories_id_identity_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.categories_id_identity_seq', 12, true);


--
-- Name: categories_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.categories_id_seq', 10, false);


--
-- Name: deliveryorders_orderid_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.deliveryorders_orderid_seq', 1, false);


--
-- Name: orderdetails_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.orderdetails_id_seq', 1, false);


--
-- Name: orderitems_id_identity_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.orderitems_id_identity_seq', 202, true);


--
-- Name: orderitems_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.orderitems_id_seq', 65, false);


--
-- Name: orders_id_identity_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.orders_id_identity_seq', 136, true);


--
-- Name: orders_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.orders_id_seq', 44, false);


--
-- Name: paymentmethods_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.paymentmethods_id_seq', 8, true);


--
-- Name: product_retail_prices_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.product_retail_prices_id_seq', 17, true);


--
-- Name: products_id_identity_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.products_id_identity_seq', 40, true);


--
-- Name: products_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.products_id_seq', 27, false);


--
-- Name: sale_items_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.sale_items_id_seq', 361, true);


--
-- Name: sale_payments_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.sale_payments_id_seq', 38, true);


--
-- Name: sales_days_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.sales_days_id_seq', 8, true);


--
-- Name: sales_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.sales_id_seq', 50, true);


--
-- Name: securitylogs_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.securitylogs_id_seq', 1, false);


--
-- Name: storesettings_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.storesettings_id_seq', 1, true);


--
-- Name: users_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.users_id_seq', 36, true);


--
-- Name: visits_id_seq; Type: SEQUENCE SET; Schema: public; Owner: -
--

SELECT pg_catalog.setval('public.visits_id_seq', 444, true);


--
-- Name: UserSite AK_UserSite_UserID; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserSite"
    ADD CONSTRAINT "AK_UserSite_UserID" UNIQUE ("UserID");


--
-- Name: DataProtectionKeys PK_DataProtectionKeys; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DataProtectionKeys"
    ADD CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY ("Id");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: UserSite UserSite_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserSite"
    ADD CONSTRAINT "UserSite_pkey" PRIMARY KEY (id);


--
-- Name: cartitems cartitems_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cartitems
    ADD CONSTRAINT cartitems_pkey PRIMARY KEY (id);


--
-- Name: carts carts_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.carts
    ADD CONSTRAINT carts_pkey PRIMARY KEY (id);


--
-- Name: catalog_outbox_events catalog_outbox_events_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.catalog_outbox_events
    ADD CONSTRAINT catalog_outbox_events_pkey PRIMARY KEY (id);


--
-- Name: categories categories_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.categories
    ADD CONSTRAINT categories_pkey PRIMARY KEY (id);


--
-- Name: deliveryorders deliveryorders_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.deliveryorders
    ADD CONSTRAINT deliveryorders_pkey PRIMARY KEY (orderid);


--
-- Name: local_site_state_snapshots local_site_state_snapshots_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.local_site_state_snapshots
    ADD CONSTRAINT local_site_state_snapshots_pkey PRIMARY KEY (siteid);


--
-- Name: orderdetails orderdetails_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orderdetails
    ADD CONSTRAINT orderdetails_pkey PRIMARY KEY (id);


--
-- Name: orderitems orderitems_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orderitems
    ADD CONSTRAINT orderitems_pkey PRIMARY KEY (id);


--
-- Name: orders orders_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orders
    ADD CONSTRAINT orders_pkey PRIMARY KEY (id);


--
-- Name: paymentmethods paymentmethods_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.paymentmethods
    ADD CONSTRAINT paymentmethods_pkey PRIMARY KEY (id);


--
-- Name: product_retail_prices product_retail_prices_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.product_retail_prices
    ADD CONSTRAINT product_retail_prices_pkey PRIMARY KEY (id);


--
-- Name: products products_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT products_pkey PRIMARY KEY (id);


--
-- Name: sale_items sale_items_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT sale_items_pkey PRIMARY KEY (id);


--
-- Name: sale_payments sale_payments_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sale_payments
    ADD CONSTRAINT sale_payments_pkey PRIMARY KEY (id);


--
-- Name: sales_days sales_days_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sales_days
    ADD CONSTRAINT sales_days_pkey PRIMARY KEY (id);


--
-- Name: sales sales_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sales
    ADD CONSTRAINT sales_pkey PRIMARY KEY (id);


--
-- Name: securitylogs securitylogs_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.securitylogs
    ADD CONSTRAINT securitylogs_pkey PRIMARY KEY (id);


--
-- Name: site_state_event_receipts site_state_event_receipts_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.site_state_event_receipts
    ADD CONSTRAINT site_state_event_receipts_pkey PRIMARY KEY (deliveryid);


--
-- Name: site_state_sync_checkpoints site_state_sync_checkpoints_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.site_state_sync_checkpoints
    ADD CONSTRAINT site_state_sync_checkpoints_pkey PRIMARY KEY (siteid);


--
-- Name: storesettings storesettings_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.storesettings
    ADD CONSTRAINT storesettings_pkey PRIMARY KEY (id);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- Name: visits visits_pkey; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.visits
    ADD CONSTRAINT visits_pkey PRIMARY KEY (id);


--
-- Name: IX_cartitems_retail_price_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_cartitems_retail_price_id" ON public.cartitems USING btree (retail_price_id);


--
-- Name: IX_orderdetails_orderid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_orderdetails_orderid" ON public.orderdetails USING btree (orderid);


--
-- Name: IX_orderitems_retail_price_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_orderitems_retail_price_id" ON public.orderitems USING btree (retail_price_id);


--
-- Name: IX_orders_userid; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_orders_userid" ON public.orders USING btree (userid);


--
-- Name: IX_sale_items_retail_price_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_sale_items_retail_price_id" ON public.sale_items USING btree (retail_price_id);


--
-- Name: IX_securitylogs_userid; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_securitylogs_userid" ON public.securitylogs USING btree (userid);


--
-- Name: ix_catalog_outbox_events_entity; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_catalog_outbox_events_entity ON public.catalog_outbox_events USING btree (entity_type, entity_id);


--
-- Name: ix_catalog_outbox_events_status_next_attempt_at; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_catalog_outbox_events_status_next_attempt_at ON public.catalog_outbox_events USING btree (status, next_attempt_at);


--
-- Name: ix_orders_cancelledat; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_orders_cancelledat ON public.orders USING btree (cancelledat);


--
-- Name: ix_orders_orderdate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_orders_orderdate ON public.orders USING btree (orderdate DESC);


--
-- Name: ix_orders_status_orderdate; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_orders_status_orderdate ON public.orders USING btree (status, orderdate DESC);


--
-- Name: ix_paymentmethods_storesettingsid; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_paymentmethods_storesettingsid ON public.paymentmethods USING btree (storesettingsid);


--
-- Name: ix_product_retail_prices_active_size; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_product_retail_prices_active_size ON public.product_retail_prices USING btree (size_ml) WHERE (is_active AND (size_ml > 0) AND (price > (0)::numeric));


--
-- Name: ix_products_brand_trgm; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_products_brand_trgm ON public.products USING gin (brand public.gin_trgm_ops);


--
-- Name: ix_products_categoryid_createdat; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_products_categoryid_createdat ON public.products USING btree (categoryid, createdat DESC);


--
-- Name: ix_products_createdat; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_products_createdat ON public.products USING btree (createdat DESC);


--
-- Name: ix_products_description_trgm; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_products_description_trgm ON public.products USING gin (description public.gin_trgm_ops);


--
-- Name: ix_products_name_trgm; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_products_name_trgm ON public.products USING gin (name public.gin_trgm_ops);


--
-- Name: ix_products_total_sold; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_products_total_sold ON public.products USING btree (total_sold);


--
-- Name: ix_sale_items_product_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_sale_items_product_id ON public.sale_items USING btree (product_id);


--
-- Name: ix_sale_items_sale_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_sale_items_sale_id ON public.sale_items USING btree (sale_id);


--
-- Name: ix_sale_payments_payment_method_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_sale_payments_payment_method_id ON public.sale_payments USING btree (payment_method_id);


--
-- Name: ix_sale_payments_sale_id; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_sale_payments_sale_id ON public.sale_payments USING btree (sale_id);


--
-- Name: ix_sales_completed_effective_date; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_sales_completed_effective_date ON public.sales USING btree (status, COALESCE(completed_at, created_at));


--
-- Name: ix_sales_invoice_number; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ix_sales_invoice_number ON public.sales USING btree (invoice_number);


--
-- Name: ix_sales_sales_day_status; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_sales_sales_day_status ON public.sales USING btree (sales_day_id, status);


--
-- Name: ix_site_state_event_receipts_site_revision; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX ix_site_state_event_receipts_site_revision ON public.site_state_event_receipts USING btree (siteid, revision);


--
-- Name: ux_cartitems_cart_product_retail; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_cartitems_cart_product_retail ON public.cartitems USING btree (cartid, productid, COALESCE(retail_price_id, 0));


--
-- Name: ux_carts_userid; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_carts_userid ON public.carts USING btree (userid);


--
-- Name: ux_product_retail_prices_product_size; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_product_retail_prices_product_size ON public.product_retail_prices USING btree (product_id, size_ml);


--
-- Name: ux_sales_days_created_by_open; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_sales_days_created_by_open ON public.sales_days USING btree (created_by, status) WHERE ((status)::text = 'Open'::text);


--
-- Name: ux_users_email; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_users_email ON public.users USING btree (email);


--
-- Name: ux_users_phone; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX ux_users_phone ON public.users USING btree (phone);


--
-- Name: deliveryorders FK_deliveryorders_orders_orderid; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.deliveryorders
    ADD CONSTRAINT "FK_deliveryorders_orders_orderid" FOREIGN KEY (orderid) REFERENCES public.orders(id) ON DELETE CASCADE;


--
-- Name: cartitems fk_cart; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cartitems
    ADD CONSTRAINT fk_cart FOREIGN KEY (cartid) REFERENCES public.carts(id) ON DELETE CASCADE;


--
-- Name: cartitems fk_cartitems_retail_price; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cartitems
    ADD CONSTRAINT fk_cartitems_retail_price FOREIGN KEY (retail_price_id) REFERENCES public.product_retail_prices(id) ON DELETE SET NULL;


--
-- Name: products fk_category; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.products
    ADD CONSTRAINT fk_category FOREIGN KEY (categoryid) REFERENCES public.categories(id) ON DELETE CASCADE;


--
-- Name: orderitems fk_order; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orderitems
    ADD CONSTRAINT fk_order FOREIGN KEY (orderid) REFERENCES public.orders(id) ON DELETE CASCADE;


--
-- Name: orderdetails fk_orderdetails_order; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orderdetails
    ADD CONSTRAINT fk_orderdetails_order FOREIGN KEY (orderid) REFERENCES public.orders(id) ON DELETE CASCADE;


--
-- Name: orderitems fk_orderitems_retail_price; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orderitems
    ADD CONSTRAINT fk_orderitems_retail_price FOREIGN KEY (retail_price_id) REFERENCES public.product_retail_prices(id) ON DELETE SET NULL;


--
-- Name: paymentmethods fk_paymentmethods_storesettings; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.paymentmethods
    ADD CONSTRAINT fk_paymentmethods_storesettings FOREIGN KEY (storesettingsid) REFERENCES public.storesettings(id) ON DELETE CASCADE;


--
-- Name: cartitems fk_product_cart; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.cartitems
    ADD CONSTRAINT fk_product_cart FOREIGN KEY (productid) REFERENCES public.products(id) ON DELETE CASCADE;


--
-- Name: orderitems fk_product_order; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.orderitems
    ADD CONSTRAINT fk_product_order FOREIGN KEY (productid) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: product_retail_prices fk_product_retail_prices_product; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.product_retail_prices
    ADD CONSTRAINT fk_product_retail_prices_product FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE CASCADE;


--
-- Name: sale_items fk_sale_items_product; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT fk_sale_items_product FOREIGN KEY (product_id) REFERENCES public.products(id) ON DELETE RESTRICT;


--
-- Name: sale_items fk_sale_items_retail_price; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT fk_sale_items_retail_price FOREIGN KEY (retail_price_id) REFERENCES public.product_retail_prices(id) ON DELETE SET NULL;


--
-- Name: sale_items fk_sale_items_sale; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sale_items
    ADD CONSTRAINT fk_sale_items_sale FOREIGN KEY (sale_id) REFERENCES public.sales(id) ON DELETE CASCADE;


--
-- Name: sale_payments fk_sale_payments_payment_method; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sale_payments
    ADD CONSTRAINT fk_sale_payments_payment_method FOREIGN KEY (payment_method_id) REFERENCES public.paymentmethods(id) ON DELETE RESTRICT;


--
-- Name: sale_payments fk_sale_payments_sale; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sale_payments
    ADD CONSTRAINT fk_sale_payments_sale FOREIGN KEY (sale_id) REFERENCES public.sales(id) ON DELETE CASCADE;


--
-- Name: sales fk_sales_sales_day; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public.sales
    ADD CONSTRAINT fk_sales_sales_day FOREIGN KEY (sales_day_id) REFERENCES public.sales_days(id) ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict RG7vimMN95Xs7ktfpkMBdayrYPEfG5k2MwR0y6ORuAdIk2j9k8keM8iIvI32r8Y

