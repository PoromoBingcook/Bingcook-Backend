CREATE UNIQUE INDEX IF NOT EXISTS ux_review_userid_propertyid
    ON review(userid, propertyid)
    WHERE userid IS NOT NULL AND propertyid IS NOT NULL;
