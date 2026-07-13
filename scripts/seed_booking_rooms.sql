-- Run this after the base BookingDB.sql schema.
-- It adds columns needed by room selection/booking draft and seeds sample stays.

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

ALTER TABLE property
    ADD COLUMN IF NOT EXISTS haswifi BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS haspool BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS hasparking BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS hasac BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS hasbreakfast BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS ispetallowed BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS isselfcheckin BOOLEAN NOT NULL DEFAULT FALSE;

ALTER TABLE booking
    ADD COLUMN IF NOT EXISTS roomquantity INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS adultguest INTEGER NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS childguest INTEGER NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS selectedaddons TEXT[] NOT NULL DEFAULT ARRAY[]::TEXT[],
    ADD COLUMN IF NOT EXISTS contactfullname VARCHAR(100),
    ADD COLUMN IF NOT EXISTS contactemail VARCHAR(100),
    ADD COLUMN IF NOT EXISTS contactphone VARCHAR(20),
    ADD COLUMN IF NOT EXISTS identitynumber VARCHAR(50);

ALTER TABLE payment
    ADD COLUMN IF NOT EXISTS provider VARCHAR(50),
    ADD COLUMN IF NOT EXISTS transactioncode VARCHAR(100),
    ADD COLUMN IF NOT EXISTS checkouturl TEXT,
    ADD COLUMN IF NOT EXISTS qrcode TEXT,
    ADD COLUMN IF NOT EXISTS paidat TIMESTAMP,
    ADD COLUMN IF NOT EXISTS updatedat TIMESTAMP;

CREATE UNIQUE INDEX IF NOT EXISTS ix_payment_transactioncode
    ON payment(transactioncode)
    WHERE transactioncode IS NOT NULL;


INSERT INTO propertytype (id, name)
VALUES
    ('01111111-1111-1111-1111-111111111111', 'Hotel'),
    ('02222222-2222-2222-2222-222222222222', 'Resort'),
    ('03333333-3333-3333-3333-333333333333', 'Apartment')
ON CONFLICT (name) DO NOTHING;

INSERT INTO property (
    id,
    typeid,
    name,
    description,
    address,
    city,
    latitude,
    longitude,
    amenities,
    pricepernight,
    rating,
    status,
    haswifi,
    haspool,
    hasparking,
    hasac,
    hasbreakfast,
    ispetallowed,
    isselfcheckin)
VALUES
    (
        '11111111-1111-1111-1111-111111111111',
        (SELECT id FROM propertytype WHERE name = 'Hotel' LIMIT 1),
        'BingCook Central Hotel',
        'Modern hotel near downtown restaurants and transit.',
        '12 Nguyen Hue Street',
        'Ho Chi Minh City',
        10.7756000,
        106.7019000,
        ARRAY['Wi-Fi', 'Parking', 'AC', 'Breakfast'],
        850000,
        4.7,
        'Active',
        TRUE,
        FALSE,
        TRUE,
        TRUE,
        TRUE,
        FALSE,
        TRUE
    ),
    (
        '12222222-2222-2222-2222-222222222222',
        (SELECT id FROM propertytype WHERE name = 'Resort' LIMIT 1),
        'BingCook Garden Resort',
        'Quiet resort with pool, garden paths, and family rooms.',
        '88 Tran Phu Beach Road',
        'Da Nang',
        16.0678000,
        108.2208000,
        ARRAY['Wi-Fi', 'Pool', 'Parking', 'AC', 'Breakfast', 'Pet friendly'],
        1450000,
        4.8,
        'Active',
        TRUE,
        TRUE,
        TRUE,
        TRUE,
        TRUE,
        TRUE,
        FALSE
    ),
    (
        '13333333-3333-3333-3333-333333333333',
        (SELECT id FROM propertytype WHERE name = 'Apartment' LIMIT 1),
        'BingCook Sky Apartment',
        'Self check-in apartment for long stays and small groups.',
        '45 West Lake View',
        'Ha Noi',
        21.0583000,
        105.8317000,
        ARRAY['Wi-Fi', 'Parking', 'AC', 'Self check-in'],
        980000,
        4.6,
        'Active',
        TRUE,
        FALSE,
        TRUE,
        TRUE,
        FALSE,
        FALSE,
        TRUE
    )
ON CONFLICT (id) DO UPDATE
SET
    typeid = EXCLUDED.typeid,
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    address = EXCLUDED.address,
    city = EXCLUDED.city,
    latitude = EXCLUDED.latitude,
    longitude = EXCLUDED.longitude,
    amenities = EXCLUDED.amenities,
    pricepernight = EXCLUDED.pricepernight,
    rating = EXCLUDED.rating,
    status = EXCLUDED.status,
    haswifi = EXCLUDED.haswifi,
    haspool = EXCLUDED.haspool,
    hasparking = EXCLUDED.hasparking,
    hasac = EXCLUDED.hasac,
    hasbreakfast = EXCLUDED.hasbreakfast,
    ispetallowed = EXCLUDED.ispetallowed,
    isselfcheckin = EXCLUDED.isselfcheckin;

INSERT INTO propertyimage (id, propertyid, imageurl)
VALUES
    ('31111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'https://images.unsplash.com/photo-1566073771259-6a8506099945'),
    ('32222222-2222-2222-2222-222222222222', '12222222-2222-2222-2222-222222222222', 'https://images.unsplash.com/photo-1582719508461-905c673771fd'),
    ('33333333-3333-3333-3333-333333333333', '13333333-3333-3333-3333-333333333333', 'https://images.unsplash.com/photo-1505693416388-ac5ce068fe85')
ON CONFLICT (id) DO UPDATE
SET imageurl = EXCLUDED.imageurl;

INSERT INTO room (
    id,
    propertyid,
    name,
    price,
    capacity,
    totalroom,
    availableroom)
VALUES
    ('21111111-1111-1111-1111-111111111111', '11111111-1111-1111-1111-111111111111', 'Deluxe King Room', 850000, 3, 8, 8),
    ('21111111-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'Family Twin Room', 1250000, 4, 5, 5),
    ('22222222-1111-1111-1111-111111111111', '12222222-2222-2222-2222-222222222222', 'Garden Villa', 1450000, 4, 4, 4),
    ('22222222-2222-2222-2222-222222222222', '12222222-2222-2222-2222-222222222222', 'Poolside Suite', 1950000, 5, 3, 3),
    ('23333333-1111-1111-1111-111111111111', '13333333-3333-3333-3333-333333333333', 'One Bedroom Apartment', 980000, 3, 6, 6),
    ('23333333-2222-2222-2222-222222222222', '13333333-3333-3333-3333-333333333333', 'Two Bedroom Apartment', 1550000, 5, 4, 4)
ON CONFLICT (id) DO UPDATE
SET
    propertyid = EXCLUDED.propertyid,
    name = EXCLUDED.name,
    price = EXCLUDED.price,
    capacity = EXCLUDED.capacity,
    totalroom = EXCLUDED.totalroom,
    availableroom = EXCLUDED.availableroom;

INSERT INTO roomimage (id, roomid, imageurl)
VALUES
    ('41111111-1111-1111-1111-111111111111', '21111111-1111-1111-1111-111111111111', 'https://images.unsplash.com/photo-1590490360182-c33d57733427'),
    ('41111111-2222-2222-2222-222222222222', '21111111-2222-2222-2222-222222222222', 'https://images.unsplash.com/photo-1566665797739-1674de7a421a'),
    ('42222222-1111-1111-1111-111111111111', '22222222-1111-1111-1111-111111111111', 'https://images.unsplash.com/photo-1571896349842-33c89424de2d'),
    ('42222222-2222-2222-2222-222222222222', '22222222-2222-2222-2222-222222222222', 'https://images.unsplash.com/photo-1578683010236-d716f9a3f461'),
    ('43333333-1111-1111-1111-111111111111', '23333333-1111-1111-1111-111111111111', 'https://images.unsplash.com/photo-1522708323590-d24dbb6b0267'),
    ('43333333-2222-2222-2222-222222222222', '23333333-2222-2222-2222-222222222222', 'https://images.unsplash.com/photo-1493809842364-78817add7ffb')
ON CONFLICT (id) DO UPDATE
SET imageurl = EXCLUDED.imageurl;
